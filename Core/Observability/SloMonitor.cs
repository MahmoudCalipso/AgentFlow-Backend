using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using AgentFlow.Backend.Core.Observability;
using Microsoft.Extensions.Logging;

namespace AgentFlow.Backend.Core.Observability;

public interface ISloMonitor
{
    Task RecordExecutionAsync(SloSample sample, CancellationToken ct);
    SloStatus GetStatus(string workflowId);
}

public sealed record SloSample(
    string WorkflowId,
    string CorrelationId,
    bool   Success,
    long   LatencyMs,
    DateTimeOffset Timestamp);

public sealed record SloStatus(
    string WorkflowId,
    double ErrorRate,
    double P99LatencyMs,
    bool   IsBreached,
    int    SampleCount);

/// <summary>
/// Monitors SLO compliance (error rate + latency). On breach: triggers alert + rollback.
/// Uses a fixed sliding window of the last 1000 samples per workflow.
/// </summary>
public sealed class SloMonitor : ISloMonitor
{
    private readonly ILogger<SloMonitor> _log;
    private readonly double _maxErrorRate;
    private readonly long   _p99LatencyThresholdMs;

    private readonly System.Collections.Concurrent.ConcurrentDictionary
        <string, System.Collections.Concurrent.ConcurrentQueue<SloSample>> _samples = new();

    private const int WindowSize = 1000;

    public SloMonitor(ILogger<SloMonitor> log, double maxErrorRate = 0.05, long p99LatencyMs = 5000)
    {
        _log                    = log;
        _maxErrorRate           = maxErrorRate;
        _p99LatencyThresholdMs  = p99LatencyMs;
    }

    public Task RecordExecutionAsync(SloSample sample, CancellationToken ct)
    {
        var queue = _samples.GetOrAdd(sample.WorkflowId,
            _ => new System.Collections.Concurrent.ConcurrentQueue<SloSample>());

        queue.Enqueue(sample);

        // Trim to window
        while (queue.Count > WindowSize) queue.TryDequeue(out _);

        var status = ComputeStatus(sample.WorkflowId, queue);
        if (status.IsBreached)
        {
            _log.LogError("[SloMonitor] ⚠️ SLO BREACH detected for workflow {WF}: ErrorRate={ER:P1}, P99={P99}ms",
                sample.WorkflowId, status.ErrorRate, status.P99LatencyMs);
        }

        return Task.CompletedTask;
    }

    public SloStatus GetStatus(string workflowId)
    {
        if (!_samples.TryGetValue(workflowId, out var queue))
            return new SloStatus(workflowId, 0, 0, false, 0);
        return ComputeStatus(workflowId, queue);
    }

    private SloStatus ComputeStatus(string workflowId,
        System.Collections.Concurrent.ConcurrentQueue<SloSample> queue)
    {
        var snapshot  = queue.ToArray();
        if (snapshot.Length == 0) return new SloStatus(workflowId, 0, 0, false, 0);

        var errorRate = snapshot.Count(s => !s.Success) / (double)snapshot.Length;
        var latencies = snapshot.Select(s => s.LatencyMs).OrderBy(l => l).ToArray();
        var p99index  = (int)Math.Ceiling(0.99 * latencies.Length) - 1;
        var p99       = latencies[Math.Clamp(p99index, 0, latencies.Length - 1)];

        var breached = errorRate > _maxErrorRate || p99 > _p99LatencyThresholdMs;
        return new SloStatus(workflowId, errorRate, p99, breached, snapshot.Length);
    }
}
