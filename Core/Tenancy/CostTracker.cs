using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace AgentFlow.Backend.Core.Tenancy;

public interface ICostTracker
{
    void TrackLlmTokens(string tenantId, string workflowId, int inputTokens, int outputTokens, string model);
    void TrackApiCall(string tenantId, string workflowId, string service);
    void TrackMemoryUsage(string tenantId, string workflowId, long bytes);
    Task<CostReport> GetReportAsync(string tenantId, CancellationToken ct);
}

public sealed record CostReport(
    string TenantId,
    long TotalLlmTokens,
    long TotalApiCalls,
    long TotalMemoryMb,
    decimal EstimatedCostUsd,
    DateTimeOffset GeneratedAt);

public sealed class InMemoryCostTracker : ICostTracker
{
    private readonly ConcurrentDictionary<string, TenantMetrics> _metrics = new();
    private readonly ILogger<InMemoryCostTracker> _log;

    private static readonly decimal _costPerThousandInputTokens = 0.01m;
    private static readonly decimal _costPerThousandOutputTokens = 0.03m;
    private static readonly decimal _costPerApiCall = 0.0001m;

    public InMemoryCostTracker(ILogger<InMemoryCostTracker> log)
    {
        _log = log;
    }

    public void TrackLlmTokens(string tenantId, string workflowId, int inputTokens, int outputTokens, string model)
    {
        var metrics = _metrics.GetOrAdd(tenantId, _ => new TenantMetrics());
        Interlocked.Add(ref metrics.InputTokens, inputTokens);
        Interlocked.Add(ref metrics.OutputTokens, outputTokens);
        _log.LogDebug("Tracking {InputTokens}+{OutputTokens} tokens for tenant {TenantId} workflow {WorkflowId}", inputTokens, outputTokens, tenantId, workflowId);
    }

    public void TrackApiCall(string tenantId, string workflowId, string service)
    {
        var metrics = _metrics.GetOrAdd(tenantId, _ => new TenantMetrics());
        Interlocked.Increment(ref metrics.ApiCalls);
    }

    public void TrackMemoryUsage(string tenantId, string workflowId, long bytes)
    {
        var metrics = _metrics.GetOrAdd(tenantId, _ => new TenantMetrics());
        Interlocked.Add(ref metrics.MemoryBytes, bytes);
    }

    public Task<CostReport> GetReportAsync(string tenantId, CancellationToken ct)
    {
        if (!_metrics.TryGetValue(tenantId, out var metrics))
        {
            return Task.FromResult(new CostReport(tenantId, 0, 0, 0, 0m, DateTimeOffset.UtcNow));
        }

        var inputTokens = Interlocked.Read(ref metrics.InputTokens);
        var outputTokens = Interlocked.Read(ref metrics.OutputTokens);
        var apiCalls = Interlocked.Read(ref metrics.ApiCalls);
        var memoryMb = Interlocked.Read(ref metrics.MemoryBytes) / (1024 * 1024);

        var cost = (inputTokens / 1000m * _costPerThousandInputTokens)
                 + (outputTokens / 1000m * _costPerThousandOutputTokens)
                 + (apiCalls * _costPerApiCall);

        return Task.FromResult(new CostReport(
            tenantId,
            inputTokens + outputTokens,
            apiCalls,
            memoryMb,
            Math.Round(cost, 4),
            DateTimeOffset.UtcNow));
    }

    private sealed class TenantMetrics
    {
        public long InputTokens;
        public long OutputTokens;
        public long ApiCalls;
        public long MemoryBytes;
    }
}
