using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace AgentFlow.Backend.Core.Observability;

public interface ICostTracker
{
    Task TrackUsageAsync(string tenantId, string correlationId, ResourceMetrics metrics, CancellationToken ct);
    Task<TenantCostReport> GetReportAsync(string tenantId, CancellationToken ct);
}

public sealed record ResourceMetrics(long CpuCycles, long MemoryBytes, int ToolCalls, int Tokens);
public sealed record TenantCostReport(string TenantId, decimal TotalCost, List<UsageEntry> Usage);
public sealed record UsageEntry(string CorrelationId, decimal Cost, DateTime Timestamp);

public sealed class ExecutionCostTracker : ICostTracker
{
    private readonly ConcurrentDictionary<string, List<UsageEntry>> _db = new();
    private const decimal RatePerMemoryByte = 0.00000001m;
    private const decimal RatePerToolCall = 0.001m;

    public Task TrackUsageAsync(string tenantId, string correlationId, ResourceMetrics metrics, CancellationToken ct)
    {
        var cost = (metrics.MemoryBytes * RatePerMemoryByte) + (metrics.ToolCalls * RatePerToolCall);
        var entries = _db.GetOrAdd(tenantId, _ => new List<UsageEntry>());
        
        lock (entries)
        {
            entries.Add(new UsageEntry(correlationId, cost, DateTime.UtcNow));
        }

        return Task.CompletedTask;
    }

    public Task<TenantCostReport> GetReportAsync(string tenantId, CancellationToken ct)
    {
        if (!_db.TryGetValue(tenantId, out var entries))
            return Task.FromResult(new TenantCostReport(tenantId, 0, new()));

        lock (entries)
        {
            var total = entries.Sum(e => e.Cost);
            return Task.FromResult(new TenantCostReport(tenantId, total, entries.ToList()));
        }
    }
}
