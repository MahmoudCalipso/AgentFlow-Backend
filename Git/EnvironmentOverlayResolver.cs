using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace AgentFlow.Backend.Git;

public interface IEnvironmentOverlayResolver
{
    Task<IReadOnlyDictionary<string, string>> ResolveAsync(string environment, CancellationToken ct);
}

/// <summary>
/// Resolves environment-specific config overlays (dev/staging/prod).
/// Merges base config with environment-specific overrides for secrets, endpoints, rate limits.
/// </summary>
public sealed class EnvironmentOverlayResolver : IEnvironmentOverlayResolver
{
    private readonly IConfiguration _config;
    private readonly ILogger<EnvironmentOverlayResolver> _log;

    public EnvironmentOverlayResolver(IConfiguration config, ILogger<EnvironmentOverlayResolver> log)
    {
        _config = config;
        _log    = log;
    }

    public Task<IReadOnlyDictionary<string, string>> ResolveAsync(string environment, CancellationToken ct)
    {
        var overlay = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        // Load base section
        var baseSection = _config.GetSection("Environments:Base");
        foreach (var kv in baseSection.AsEnumerable(makePathsRelative: true))
        {
            if (kv.Value != null) overlay[kv.Key] = kv.Value;
        }

        // Override with environment-specific section
        var envSection = _config.GetSection($"Environments:{environment}");
        foreach (var kv in envSection.AsEnumerable(makePathsRelative: true))
        {
            if (kv.Value != null)
            {
                overlay[kv.Key] = kv.Value;
                _log.LogDebug("[EnvOverlay] Override '{Key}' for environment '{Env}'", kv.Key, environment);
            }
        }

        _log.LogInformation("[EnvOverlay] Resolved {Count} config keys for environment '{Env}'", overlay.Count, environment);
        return Task.FromResult<IReadOnlyDictionary<string, string>>(overlay);
    }
}
