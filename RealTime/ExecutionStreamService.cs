using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;

namespace AgentFlow.Backend.RealTime;

public sealed class ExecutionStreamService
{
    private readonly ConcurrentDictionary<string, ConcurrentQueue<ExecutionDelta>> _deltaQueues = new();
    private readonly ConcurrentDictionary<string, List<ExecutionDelta>> _history = new();
    private readonly IHubContext<ExecutionHub> _hub;
    private readonly ILogger<ExecutionStreamService> _log;

    public ExecutionStreamService(IHubContext<ExecutionHub> hub, ILogger<ExecutionStreamService> log)
    {
        _hub = hub;
        _log = log;
    }

    public async Task BroadcastDeltaAsync(string correlationId, ExecutionDelta delta, CancellationToken ct = default)
    {
        var queue = _deltaQueues.GetOrAdd(correlationId, _ => new ConcurrentQueue<ExecutionDelta>());
        queue.Enqueue(delta);

        var history = _history.GetOrAdd(correlationId, _ => new List<ExecutionDelta>());
        lock (history) { history.Add(delta); }

        await _hub.Clients.Group(correlationId).SendAsync("delta", new
        {
            correlationId,
            nodeId = delta.NodeId,
            eventType = delta.EventType,
            data = delta.Data,
            outputPort = delta.OutputPort,
            timestamp = delta.Timestamp.ToUnixTimeMilliseconds()
        }, ct);
    }

    public IReadOnlyList<ExecutionDelta> GetHistory(string correlationId)
    {
        _history.TryGetValue(correlationId, out var history);
        return history ?? new List<ExecutionDelta>();
    }

    public void ClearHistory(string correlationId)
    {
        _history.TryRemove(correlationId, out _);
        _deltaQueues.TryRemove(correlationId, out _);
    }
}
