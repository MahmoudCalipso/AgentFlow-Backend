using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.SemanticKernel.Embeddings;
using Microsoft.Extensions.Logging;

namespace AgentFlow.Backend.Memory;

public sealed class QdrantVectorClient : IQdrantClient
{
    private readonly HttpClient _http;
    private readonly ITextEmbeddingGenerationService _embedder;
    private readonly string _collection;
    private readonly ILogger<QdrantVectorClient> _log;
    private readonly JsonSerializerOptions _jsonOpts = new() { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower };

    public QdrantVectorClient(
        string endpoint,
        string? apiKey,
        string collection,
        ITextEmbeddingGenerationService embedder,
        ILogger<QdrantVectorClient> log)
    {
        _collection = collection;
        _embedder = embedder;
        _log = log;

        var handler = new HttpClientHandler();
        _http = new HttpClient(handler)
        {
            BaseAddress = new Uri(endpoint),
            Timeout = TimeSpan.FromSeconds(30)
        };

        if (!string.IsNullOrEmpty(apiKey))
        {
            _http.DefaultRequestHeaders.Add("api-key", apiKey);
        }

        EnsureCollectionAsync(CancellationToken.None).GetAwaiter().GetResult();
    }

    private async Task EnsureCollectionAsync(CancellationToken ct)
    {
        try
        {
            var resp = await _http.GetAsync($"/collections/{_collection}", ct);
            if (resp.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                var body = JsonSerializer.Serialize(new
                {
                    vectors = new { size = 1536, distance = "Cosine" }
                });
                var content = new StringContent(body, Encoding.UTF8, "application/json");
                await _http.PutAsync($"/collections/{_collection}", content, ct);
                _log.LogInformation("Created Qdrant collection {Collection}", _collection);
            }
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "Could not ensure Qdrant collection {Collection} exists", _collection);
        }
    }

    public async Task StorePatternAsync(MigrationPattern pattern, CancellationToken ct)
    {
        var text = $"{pattern.OriginalCode}\n---\n{pattern.MigratedCode}\n{pattern.MigrationContext}";
        var embeddings = await _embedder.GenerateEmbeddingsAsync(new[] { text }, cancellationToken: ct);
        var vector = embeddings[0].ToArray();

        var point = new
        {
            id = pattern.Id.ToString("N"),
            vector,
            payload = new
            {
                original_code = pattern.OriginalCode,
                migrated_code = pattern.MigratedCode,
                context = pattern.MigrationContext,
                confidence = pattern.Confidence,
                usage_count = pattern.UsageCount,
                created_at = pattern.CreatedAt.ToUnixTimeSeconds(),
                tags = pattern.Tags
            }
        };

        var body = JsonSerializer.Serialize(new { points = new[] { point } }, _jsonOpts);
        var content = new StringContent(body, Encoding.UTF8, "application/json");
        var resp = await _http.PutAsync($"/collections/{_collection}/points", content, ct);
        resp.EnsureSuccessStatusCode();

        _log.LogDebug("Stored pattern {PatternId} in Qdrant", pattern.Id);
    }

    public async Task<IReadOnlyList<MigrationPattern>> SearchPatternsAsync(PatternQuery query, CancellationToken ct)
    {
        var embeddings = await _embedder.GenerateEmbeddingsAsync(new[] { query.Text }, cancellationToken: ct);
        var vector = embeddings[0].ToArray();

        var body = JsonSerializer.Serialize(new
        {
            vector,
            limit = query.Limit,
            with_payload = true,
            score_threshold = query.MinConfidence
        }, _jsonOpts);

        var content = new StringContent(body, Encoding.UTF8, "application/json");
        var resp = await _http.PostAsync($"/collections/{_collection}/points/search", content, ct);
        resp.EnsureSuccessStatusCode();

        var json = await resp.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(json);
        var results = new List<MigrationPattern>();

        if (doc.RootElement.TryGetProperty("result", out var resultArr))
        {
            foreach (var item in resultArr.EnumerateArray())
            {
                if (!item.TryGetProperty("payload", out var payload)) continue;

                results.Add(new MigrationPattern
                {
                    OriginalCode = payload.TryGetProperty("original_code", out var oc) ? oc.GetString() ?? "" : "",
                    MigratedCode = payload.TryGetProperty("migrated_code", out var mc) ? mc.GetString() ?? "" : "",
                    MigrationContext = payload.TryGetProperty("context", out var ctx) ? ctx.GetString() ?? "" : "",
                    Confidence = payload.TryGetProperty("confidence", out var conf) ? conf.GetSingle() : 0f,
                    UsageCount = payload.TryGetProperty("usage_count", out var uc) ? uc.GetInt32() : 0
                });
            }
        }

        return results;
    }

    public async Task DeletePatternAsync(Guid id, CancellationToken ct)
    {
        var body = JsonSerializer.Serialize(new { points = new[] { id.ToString("N") } });
        var content = new StringContent(body, Encoding.UTF8, "application/json");
        var resp = await _http.PostAsync($"/collections/{_collection}/points/delete", content, ct);
        resp.EnsureSuccessStatusCode();
    }
}
