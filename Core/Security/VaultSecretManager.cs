using System;
using System.Collections.Concurrent;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace AgentFlow.Backend.Core.Security;

public interface ISecretManager
{
    Task<string> GetSecretAsync(string path, CancellationToken ct);
}

public sealed record VaultResponse(VaultData Data);
public sealed record VaultData(System.Collections.Generic.Dictionary<string, object> Data, VaultMetadata Metadata);
public sealed record VaultMetadata(double LeaseDuration);

public sealed record CachedSecret(string Value, DateTimeOffset ExpireAt)
{
    public bool IsExpired => DateTimeOffset.UtcNow >= ExpireAt;
}

public sealed class VaultSecretManager : ISecretManager
{
    private readonly IHttpClientFactory _http;
    private readonly string? _vaultToken;
    private readonly string? _vaultAddr;
    private readonly ConcurrentDictionary<string, CachedSecret> _cache = new();
    private readonly ILogger<VaultSecretManager> _log;
    private static readonly TimeSpan _minLeaseBuffer = TimeSpan.FromMinutes(5);

    public VaultSecretManager(IHttpClientFactory http, IConfiguration config, ILogger<VaultSecretManager> log)
    {
        _http = http;
        _vaultToken = config["Vault:Token"] ?? Environment.GetEnvironmentVariable("VAULT_TOKEN");
        _vaultAddr = config["Vault:Address"] ?? Environment.GetEnvironmentVariable("VAULT_ADDR");
        _log = log;
    }

    public async Task<string> GetSecretAsync(string path, CancellationToken ct)
    {
        if (_cache.TryGetValue(path, out var cached) && !cached.IsExpired)
        {
            return cached.Value;
        }

        if (!string.IsNullOrEmpty(_vaultAddr) && !string.IsNullOrEmpty(_vaultToken))
        {
            try
            {
                var client = _http.CreateClient("vault");
                client.BaseAddress = new Uri(_vaultAddr);
                client.DefaultRequestHeaders.Add("X-Vault-Token", _vaultToken);
                client.Timeout = TimeSpan.FromSeconds(10);

                using var resp = await client.GetAsync($"/v1/secret/data/{path}", ct);
                if (resp.IsSuccessStatusCode)
                {
                    var json = await resp.Content.ReadAsStringAsync(ct);
                    using var doc = JsonDocument.Parse(json);
                    if (doc.RootElement.TryGetProperty("data", out var dataEl)
                        && dataEl.TryGetProperty("data", out var innerData)
                        && innerData.TryGetProperty("value", out var valEl))
                    {
                        var secret = valEl.GetString() ?? string.Empty;
                        double leaseSecs = 3600;
                        if (doc.RootElement.TryGetProperty("lease_duration", out var ld))
                        {
                            leaseSecs = ld.GetDouble();
                        }
                        var ttl = TimeSpan.FromSeconds(Math.Max(0, leaseSecs)) - _minLeaseBuffer;
                        if (ttl < TimeSpan.FromSeconds(30)) ttl = TimeSpan.FromSeconds(30);
                        _cache[path] = new CachedSecret(secret, DateTimeOffset.UtcNow.Add(ttl));
                        _log.LogDebug("Loaded secret from Vault: {Path}, expires in {TTL}", path, ttl);
                        return secret;
                    }
                }
                else
                {
                    _log.LogWarning("Vault returned {StatusCode} for path {Path}", resp.StatusCode, path);
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _log.LogWarning(ex, "Failed to fetch secret from Vault at path {Path}, falling back to environment", path);
            }
        }

        var envValue = Environment.GetEnvironmentVariable(path.Replace("/", "__").Replace("-", "_").ToUpperInvariant()) ?? string.Empty;
        if (!string.IsNullOrEmpty(envValue))
        {
            _cache[path] = new CachedSecret(envValue, DateTimeOffset.UtcNow.AddMinutes(5));
        }

        return envValue;
    }
}
