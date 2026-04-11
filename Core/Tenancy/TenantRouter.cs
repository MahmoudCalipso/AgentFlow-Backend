using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace AgentFlow.Backend.Core.Tenancy;

public interface ITenantRouter
{
    string? ExtractTenantId(HttpContext context);
    Task<bool> IsQuotaExceededAsync(string tenantId, CancellationToken ct);
    Task RecordRequestAsync(string tenantId, CancellationToken ct);
}

public sealed class TenantRouter : ITenantRouter
{
    private readonly ILogger<TenantRouter> _log;
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, TenantUsage> _usage = new();
    private readonly int _defaultRateLimitPerMinute = 100;

    public TenantRouter(ILogger<TenantRouter> log)
    {
        _log = log;
    }

    public string? ExtractTenantId(HttpContext context)
    {
        if (context.Request.Headers.TryGetValue("X-Tenant-Id", out var tenantId))
        {
            return tenantId.ToString();
        }

        var user = context.User;
        var tenantClaim = user?.FindFirst("tenant_id");
        return tenantClaim?.Value;
    }

    public async Task<bool> IsQuotaExceededAsync(string tenantId, CancellationToken ct)
    {
        var usage = _usage.GetOrAdd(tenantId, _ => new TenantUsage());
        usage.PruneOldRequests(TimeSpan.FromMinutes(1));
        bool exceeded = usage.RequestsInWindow >= _defaultRateLimitPerMinute;
        if (exceeded)
        {
            _log.LogWarning("Tenant {TenantId} has exceeded rate limit ({Count}/{Limit})", tenantId, usage.RequestsInWindow, _defaultRateLimitPerMinute);
        }
        await Task.CompletedTask;
        return exceeded;
    }

    public async Task RecordRequestAsync(string tenantId, CancellationToken ct)
    {
        var usage = _usage.GetOrAdd(tenantId, _ => new TenantUsage());
        usage.RecordRequest();
        await Task.CompletedTask;
    }

    private sealed class TenantUsage
    {
        private readonly Queue<DateTimeOffset> _requests = new();
        private readonly object _lock = new();

        public int RequestsInWindow
        {
            get { lock (_lock) return _requests.Count; }
        }

        public void RecordRequest()
        {
            lock (_lock) { _requests.Enqueue(DateTimeOffset.UtcNow); }
        }

        public void PruneOldRequests(TimeSpan window)
        {
            var cutoff = DateTimeOffset.UtcNow - window;
            lock (_lock)
            {
                while (_requests.Count > 0 && _requests.Peek() < cutoff)
                    _requests.Dequeue();
            }
        }
    }
}
