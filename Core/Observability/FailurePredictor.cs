using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace AgentFlow.Backend.Core.Observability;

public interface IFailurePredictor
{
    Task<FailurePrediction> PredictAsync(string workflowId, string nodeId, CancellationToken ct);
    Task RecordOutcomeAsync(string workflowId, string nodeId, bool failed, CancellationToken ct);
}

public sealed record FailurePrediction(
    string WorkflowId,
    string NodeId,
    double FailureProbability,
    string RiskLevel,
    string Recommendation);

/// <summary>
/// Lightweight ML failure predictor using rolling failure rates + exponential smoothing.
/// High-risk executions are flagged before they run, enabling proactive intervention.
/// </summary>
public sealed class FailurePredictor : IFailurePredictor
{
    private readonly ILogger<FailurePredictor> _log;

    private sealed record NodeStats(double FailureRate, int TotalExecutions, DateTimeOffset LastFailure);

    private readonly ConcurrentDictionary<string, NodeStats> _stats = new();

    // EMA smoothing factor (0 = slow, 1 = immediate)
    private const double Alpha = 0.2;

    public FailurePredictor(ILogger<FailurePredictor> log) => _log = log;

    public Task<FailurePrediction> PredictAsync(string workflowId, string nodeId, CancellationToken ct)
    {
        var key   = $"{workflowId}:{nodeId}";
        var stats = _stats.GetValueOrDefault(key, new NodeStats(0.0, 0, DateTimeOffset.MinValue));

        // Boost risk if recent failure
        var recencyBoost = (DateTimeOffset.UtcNow - stats.LastFailure).TotalMinutes < 10 ? 0.15 : 0.0;
        var probability  = Math.Clamp(stats.FailureRate + recencyBoost, 0.0, 1.0);

        var riskLevel = probability switch
        {
            >= 0.7 => "CRITICAL",
            >= 0.4 => "HIGH",
            >= 0.2 => "MEDIUM",
            _      => "LOW"
        };

        var recommendation = riskLevel switch
        {
            "CRITICAL" => "Abort and alert operator. Recent repeated failures detected.",
            "HIGH"     => "Add retry with exponential backoff. Consider circuit breaker.",
            "MEDIUM"   => "Monitor closely. Enable enhanced logging.",
            _          => "Normal. No action required."
        };

        if (riskLevel is "CRITICAL" or "HIGH")
            _log.LogWarning("[FailurePredictor] {Risk} risk for {WF}/{Node}: {Prob:P1}", riskLevel, workflowId, nodeId, probability);

        return Task.FromResult(new FailurePrediction(workflowId, nodeId, probability, riskLevel, recommendation));
    }

    public Task RecordOutcomeAsync(string workflowId, string nodeId, bool failed, CancellationToken ct)
    {
        var key = $"{workflowId}:{nodeId}";
        _stats.AddOrUpdate(key,
            _ => new NodeStats(failed ? 1.0 : 0.0, 1, failed ? DateTimeOffset.UtcNow : DateTimeOffset.MinValue),
            (_, prev) =>
            {
                var newRate = Alpha * (failed ? 1.0 : 0.0) + (1 - Alpha) * prev.FailureRate;
                return new NodeStats(newRate, prev.TotalExecutions + 1, failed ? DateTimeOffset.UtcNow : prev.LastFailure);
            });
        return Task.CompletedTask;
    }
}
