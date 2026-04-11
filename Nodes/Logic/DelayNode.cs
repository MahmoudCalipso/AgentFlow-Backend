using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AgentFlow.Backend.Core.Nodes;
using AgentFlow.Backend.Core.Execution;
using Microsoft.Extensions.Logging;

namespace AgentFlow.Backend.Nodes.Logic;

public sealed class DelayNode : INodeHandler
{
    private readonly ILogger<DelayNode> _log;
    public string NodeId { get; }

    public DelayNode(string nodeId, ILogger<DelayNode> log)
    {
        NodeId = nodeId;
        _log   = log;
    }

    public async ValueTask<NodeResult> HandleAsync(NodeContext ctx, CancellationToken ct)
    {
        var ms  = ctx.GetConfig<int>(NodeId, "duration_ms", 1000);
        var max = 3_600_000; // 1-hour cap
        if (ms > max)
        {
            _log.LogWarning("[DelayNode] Clamping {Req}ms to {Max}ms", ms, max);
            ms = max;
        }

        _log.LogInformation("[DelayNode] Sleeping {Ms}ms (correlation: {CorrId})", ms, ctx.CorrelationId);
        await Task.Delay(ms, ct);

        return NodeResult.Ok(new List<IReadOnlyList<ExecutionItem>> { ctx.InputItems });
    }
}
