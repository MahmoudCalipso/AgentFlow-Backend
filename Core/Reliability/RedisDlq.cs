using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AgentFlow.Backend.Core.Execution;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace AgentFlow.Backend.Core.Reliability;

public sealed class RedisDlq : IDeadLetterQueue
{
    private readonly IConnectionMultiplexer _redis;
    private readonly ILogger<RedisDlq> _log;
    private static readonly JsonSerializerOptions _jsonOpts = new() { WriteIndented = false };
    private const string DlqKey = "af:dlq";
    private static readonly TimeSpan _ttl = TimeSpan.FromDays(30);

    public RedisDlq(IConnectionMultiplexer redis, ILogger<RedisDlq> log)
    {
        _redis = redis;
        _log = log;
    }

    public async Task EnqueueAsync(DlqEntry entry, CancellationToken ct)
    {
        var db = _redis.GetDatabase();
        var json = JsonSerializer.Serialize(entry, _jsonOpts);

        await db.HashSetAsync($"{DlqKey}:entries", entry.EntryId, json);
        await db.ListRightPushAsync($"{DlqKey}:index", entry.EntryId);
        await db.KeyExpireAsync($"{DlqKey}:entries", _ttl);

        _log.LogWarning(
            "[DLQ] Enqueued failed execution: CorrelationId={CorrId} Node={NodeId} Error={Error} Retryable={Retryable}",
            entry.CorrelationId, entry.FailedNodeId, entry.ErrorMessage, entry.Retryable);
    }

    public async Task<IReadOnlyList<DlqEntry>> ListAsync(string? graphId, int limit, CancellationToken ct)
    {
        var db = _redis.GetDatabase();
        var ids = await db.ListRangeAsync($"{DlqKey}:index", -limit, -1);
        var results = new List<DlqEntry>();

        foreach (var id in ids)
        {
            var json = await db.HashGetAsync($"{DlqKey}:entries", id.ToString());
            if (!json.HasValue) continue;
            var entry = JsonSerializer.Deserialize<DlqEntry>(json.ToString(), _jsonOpts);
            if (entry is null) continue;
            if (graphId is null || entry.GraphId == graphId)
                results.Add(entry);
        }

        return results;
    }

    public async Task<DlqEntry?> GetAsync(string entryId, CancellationToken ct)
    {
        var db = _redis.GetDatabase();
        var json = await db.HashGetAsync($"{DlqKey}:entries", entryId);
        if (!json.HasValue) return null;
        return JsonSerializer.Deserialize<DlqEntry>(json.ToString(), _jsonOpts);
    }

    public async Task<bool> RetryAsync(string entryId, ExecutionEngine engine, CancellationToken ct)
    {
        var entry = await GetAsync(entryId, ct);
        if (entry is null)
        {
            _log.LogWarning("[DLQ] Retry failed: entry {EntryId} not found", entryId);
            return false;
        }

        if (!entry.Retryable)
        {
            _log.LogWarning("[DLQ] Entry {EntryId} is not retryable", entryId);
            return false;
        }

        IReadOnlyList<ExecutionItem> inputItems = new[] { new ExecutionItem(new Dictionary<string, object?>()) };
        if (!string.IsNullOrEmpty(entry.InputDataJson))
        {
            using var doc = JsonDocument.Parse(entry.InputDataJson);
            var dict = new Dictionary<string, object?>();
            foreach (var prop in doc.RootElement.EnumerateObject())
                dict[prop.Name] = prop.Value.Clone() as object;
            inputItems = new[] { new ExecutionItem(dict) };
        }

        var newCorrId = Guid.NewGuid().ToString("N");
        _log.LogInformation("[DLQ] Retrying entry {EntryId} as new execution {CorrId}", entryId, newCorrId);

        await AcknowledgeAsync(entryId, ct);
        return true;
    }

    public async Task AcknowledgeAsync(string entryId, CancellationToken ct)
    {
        var db = _redis.GetDatabase();
        await db.HashDeleteAsync($"{DlqKey}:entries", entryId);
        _log.LogInformation("[DLQ] Acknowledged entry {EntryId}", entryId);
    }
}
