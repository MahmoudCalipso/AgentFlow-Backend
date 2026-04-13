using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace AgentFlow.Backend.Marketplace;

public sealed record NodePackage(
    string Id,
    string Version,
    string Author,
    string Description,
    string DownloadUrl,
    string ChecksumSha256,
    string Signature,
    string[] Dependencies);

public sealed class MarketplaceRegistry
{
    private readonly IHttpClientFactory _http;
    private readonly ILogger<MarketplaceRegistry> _log;
    private readonly string _baseUrl;

    public MarketplaceRegistry(IHttpClientFactory http, Microsoft.Extensions.Logging.ILogger<MarketplaceRegistry> log, Microsoft.Extensions.Configuration.IConfiguration config)
    {
        _http = http;
        _log = log;
        _baseUrl = config["AgentFlow:CommunityNodesUrl"] ?? "https://raw.githubusercontent.com/MahmoudCalipso/community-nodes/main/packages";
    }

    public async Task<JsonElement> GetIndexAsync(CancellationToken ct)
    {
        var client = _http.CreateClient("agentflow-default");
        var indexUrl = $"{_baseUrl}/index.json";
        using var resp = await client.GetAsync(indexUrl, ct);
        resp.EnsureSuccessStatusCode();
        var json = await resp.Content.ReadAsStringAsync(ct);
        return JsonSerializer.Deserialize<JsonElement>(json, AgentFlowJsonContext.Default.JsonElement);
    }

    public async Task<NodePackage> DownloadAndVerifyAsync(string packageId, string version, CancellationToken ct)
    {
        var client = _http.CreateClient("agentflow-default");
        _log.LogInformation("Fetching metadata for marketplace package {PackageId}@{Version}", packageId, version);

        var manifestUrl = $"{_baseUrl}/{packageId}/{version}/manifest.json";
        using var manifestResp = await client.GetAsync(manifestUrl, ct);
        if (!manifestResp.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"Package {packageId}@{version} not found in the community registry (HTTP {(int)manifestResp.StatusCode}).");
        }

        var manifestJson = await manifestResp.Content.ReadAsStringAsync(ct);
        var package = JsonSerializer.Deserialize<NodePackage>(manifestJson, AgentFlowJsonContext.Default.NodePackage)
            ?? throw new InvalidOperationException("Invalid package manifest.");

        // In the new service-based architecture, we no longer install files locally.
        // We simply return the package information for the UI to display or the orchestrator to use.
        _log.LogInformation("Metadata for package {PackageId}@{Version} retrieved successfully", packageId, version);

        return package;
    }
}
