using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using OpenTelemetry;

namespace AgentFlow.Backend.Core.Observability;

public interface IAuditLogger
{
    Task LogStartAsync(string nodeId, string correlationId);
    Task LogSuccessAsync(string nodeId, string correlationId, object? result);
    Task LogFailureAsync(string nodeId, string correlationId, string error);
    Task LogEventAsync(string nodeId, string correlationId, string eventName, object? data = null);
}

public sealed class OpenTelemetryAuditLogger : IAuditLogger
{
    private static readonly ActivitySource _activitySource = new("AgentFlow.Backend", "1.0.0");
    private readonly ILogger<OpenTelemetryAuditLogger> _log;

    public OpenTelemetryAuditLogger(ILogger<OpenTelemetryAuditLogger> log)
    {
        _log = log;
    }

    public Task LogStartAsync(string nodeId, string correlationId)
    {
        _log.LogInformation(
            "[AUDIT] Node={NodeId} CorrelationId={CorrelationId} Event=NodeStart Timestamp={Timestamp}",
            nodeId, correlationId, DateTimeOffset.UtcNow);

        using var activity = _activitySource.StartActivity($"NodeExecute:{nodeId}");
        activity?.SetTag("agentflow.node_id", nodeId);
        activity?.SetTag("agentflow.correlation_id", correlationId);
        activity?.SetTag("agentflow.event", "start");

        return Task.CompletedTask;
    }

    public Task LogSuccessAsync(string nodeId, string correlationId, object? result)
    {
        _log.LogInformation(
            "[AUDIT] Node={NodeId} CorrelationId={CorrelationId} Event=NodeSuccess Timestamp={Timestamp}",
            nodeId, correlationId, DateTimeOffset.UtcNow);

        using var activity = _activitySource.StartActivity($"NodeResult:{nodeId}");
        activity?.SetTag("agentflow.node_id", nodeId);
        activity?.SetTag("agentflow.correlation_id", correlationId);
        activity?.SetTag("agentflow.event", "success");
        activity?.SetStatus(ActivityStatusCode.Ok);

        return Task.CompletedTask;
    }

    public Task LogFailureAsync(string nodeId, string correlationId, string error)
    {
        _log.LogError(
            "[AUDIT] Node={NodeId} CorrelationId={CorrelationId} Event=NodeFailure Error={Error} Timestamp={Timestamp}",
            nodeId, correlationId, error, DateTimeOffset.UtcNow);

        using var activity = _activitySource.StartActivity($"NodeError:{nodeId}");
        activity?.SetTag("agentflow.node_id", nodeId);
        activity?.SetTag("agentflow.correlation_id", correlationId);
        activity?.SetTag("agentflow.event", "failure");
        activity?.SetTag("agentflow.error", error);
        activity?.SetStatus(ActivityStatusCode.Error, error);

        return Task.CompletedTask;
    }

    public Task LogEventAsync(string nodeId, string correlationId, string eventName, object? data = null)
    {
        _log.LogInformation(
            "[AUDIT] Node={NodeId} CorrelationId={CorrelationId} Event={EventName} Timestamp={Timestamp}",
            nodeId, correlationId, eventName, DateTimeOffset.UtcNow);

        return Task.CompletedTask;
    }
}
