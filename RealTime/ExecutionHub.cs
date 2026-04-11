using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AgentFlow.Backend.Core.Execution;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;

namespace AgentFlow.Backend.RealTime;

public sealed class ExecutionHub : Hub
{
    private readonly ILogger<ExecutionHub> _log;

    public ExecutionHub(ILogger<ExecutionHub> log)
    {
        _log = log;
    }

    public async Task Subscribe(string correlationId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, correlationId);
        _log.LogDebug("Client {ConnectionId} subscribed to {CorrelationId}", Context.ConnectionId, correlationId);
    }

    public async Task Unsubscribe(string correlationId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, correlationId);
        _log.LogDebug("Client {ConnectionId} unsubscribed from {CorrelationId}", Context.ConnectionId, correlationId);
    }

    public override async Task OnConnectedAsync()
    {
        _log.LogDebug("Client connected: {ConnectionId}", Context.ConnectionId);
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        _log.LogDebug("Client disconnected: {ConnectionId}", Context.ConnectionId);
        await base.OnDisconnectedAsync(exception);
    }
}
