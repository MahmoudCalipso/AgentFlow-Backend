using System;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
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
    private readonly string _cacheDir;

    public MarketplaceRegistry(IHttpClientFactory http, ILogger<MarketplaceRegistry> log)
    {
        _http = http;
        _log = log;
        _cacheDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "agentflow", "packages");
        Directory.CreateDirectory(_cacheDir);
    }

    public async Task<JsonElement> GetIndexAsync(CancellationToken ct)
    {
        var client = _http.CreateClient("agentflow-default");
        var indexUrl = "https://raw.githubusercontent.com/MahmoudCalipso/community-nodes/main/packages/index.json";
        using var resp = await client.GetAsync(indexUrl, ct);
        resp.EnsureSuccessStatusCode();
        var json = await resp.Content.ReadAsStringAsync(ct);
        return JsonSerializer.Deserialize<JsonElement>(json);
    }

    public async Task<NodePackage> DownloadAndVerifyAsync(string packageId, string version, CancellationToken ct)
    {
        var client = _http.CreateClient("agentflow-default");
        _log.LogInformation("Downloading marketplace package {PackageId}@{Version}", packageId, version);

        // Solution: Use a free GitHub repository as the community registry!
        // Anyone can contribute nodes by opening a PR to this repo. AgentFlow natively downloads the manifest directly from GitHub.
        var manifestUrl = $"https://raw.githubusercontent.com/MahmoudCalipso/community-nodes/main/packages/{packageId}/{version}/manifest.json";
        using var manifestResp = await client.GetAsync(manifestUrl, ct);
        if (!manifestResp.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"Package {packageId}@{version} not found in the GitHub Community Registry (HTTP {(int)manifestResp.StatusCode}).");
        }

        var manifestJson = await manifestResp.Content.ReadAsStringAsync(ct);
        var package = JsonSerializer.Deserialize<NodePackage>(manifestJson)
            ?? throw new InvalidOperationException("Invalid package manifest.");

        var cachedPath = Path.Combine(_cacheDir, $"{packageId}-{version}.zip");

        if (!File.Exists(cachedPath))
        {
            using var packageResp = await client.GetAsync(package.DownloadUrl, ct);
            packageResp.EnsureSuccessStatusCode();
            var bytes = await packageResp.Content.ReadAsByteArrayAsync(ct);

            VerifyChecksum(bytes, package.ChecksumSha256, packageId, version);

            await File.WriteAllBytesAsync(cachedPath, bytes, ct);
            _log.LogInformation("Package {PackageId}@{Version} downloaded and verified", packageId, version);
        }
        else
        {
            var cached = await File.ReadAllBytesAsync(cachedPath, ct);
            VerifyChecksum(cached, package.ChecksumSha256, packageId, version);
            _log.LogDebug("Package {PackageId}@{Version} loaded from cache", packageId, version);
        }

        return package;
    }

    private void VerifyChecksum(byte[] data, string expectedSha256, string packageId, string version)
    {
        var actualHash = Convert.ToHexString(SHA256.HashData(data)).ToLowerInvariant();
        var expected = expectedSha256.ToLowerInvariant();

        if (!actualHash.Equals(expected, StringComparison.Ordinal))
        {
            _log.LogError("Checksum mismatch for {PackageId}@{Version}: expected {Expected}, got {Actual}", packageId, version, expected, actualHash);
            throw new CryptographicException($"Package {packageId}@{version} failed integrity check. Download may be corrupted or tampered.");
        }
    }
}
