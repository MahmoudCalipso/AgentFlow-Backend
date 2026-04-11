using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace AgentFlow.Backend.Core.Geo;

public interface IGeoRouter
{
    Task<string> ResolveRegionAsync(string tenantId, string preferredRegion, CancellationToken ct);
    bool IsRegionAllowed(string tenantId, string region);
}

public sealed class GeoRouter : IGeoRouter
{
    private readonly ILogger<GeoRouter> _log;

    // Tenant → approved region set. Loaded from config/policy store in production.
    private readonly Dictionary<string, HashSet<string>> _tenantRegions = new(StringComparer.OrdinalIgnoreCase);

    // GDPR: EU-only tenants; HIPAA: US-only; etc.
    private static readonly Dictionary<string, string[]> _complianceDefaults = new(StringComparer.OrdinalIgnoreCase)
    {
        ["GDPR"]  = new[] { "eu-west-1", "eu-central-1", "eu-north-1" },
        ["HIPAA"] = new[] { "us-east-1", "us-east-2", "us-west-2" },
        ["APAC"]  = new[] { "ap-southeast-1", "ap-northeast-1", "ap-south-1" },
    };

    public GeoRouter(ILogger<GeoRouter> log) => _log = log;

    public void SetTenantRegions(string tenantId, IEnumerable<string> allowedRegions)
    {
        _tenantRegions[tenantId] = new HashSet<string>(allowedRegions, StringComparer.OrdinalIgnoreCase);
        _log.LogInformation("[GeoRouter] Tenant {Tenant} regions set: {Regions}", tenantId, string.Join(", ", allowedRegions));
    }

    public bool IsRegionAllowed(string tenantId, string region)
    {
        if (!_tenantRegions.TryGetValue(tenantId, out var allowed)) return true; // default: allow all
        return allowed.Contains(region);
    }

    public Task<string> ResolveRegionAsync(string tenantId, string preferredRegion, CancellationToken ct)
    {
        if (IsRegionAllowed(tenantId, preferredRegion))
        {
            _log.LogDebug("[GeoRouter] Tenant {T} → preferred region {R} approved", tenantId, preferredRegion);
            return Task.FromResult(preferredRegion);
        }

        if (_tenantRegions.TryGetValue(tenantId, out var allowed) && allowed.Count > 0)
        {
            var fallback = System.Linq.Enumerable.First(allowed);
            _log.LogWarning("[GeoRouter] Tenant {T}: preferred region '{P}' not allowed. Routing to '{F}'",
                tenantId, preferredRegion, fallback);
            return Task.FromResult(fallback);
        }

        _log.LogWarning("[GeoRouter] Tenant {T} has no region policy. Defaulting to '{R}'", tenantId, preferredRegion);
        return Task.FromResult(preferredRegion);
    }
}
