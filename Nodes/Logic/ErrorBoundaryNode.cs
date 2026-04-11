using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AgentFlow.Backend.Core.Nodes;
using AgentFlow.Backend.Core.Execution;
using Microsoft.Extensions.Logging;

namespace AgentFlow.Backend.Nodes.Logic;

public sealed class ErrorBoundaryNode : INodeHandler
{
    private readonly ILogger<ErrorBoundaryNode> _log;
    public string NodeId { get; }

    public ErrorBoundaryNode(string nodeId, ILogger<ErrorBoundaryNode> log)
    {
        NodeId = nodeId;
        _log   = log;
    }

    public ValueTask<NodeResult> HandleAsync(NodeContext ctx, CancellationToken ct)
    {
        // Pass-through success items on output 0.
        // Any items already tagged with error context route to output 1.
        var success = new List<ExecutionItem>();
        var errors  = new List<ExecutionItem>();

        foreach (var item in ctx.InputItems)
        {
            if (item.Data.ContainsKey("error_message"))
            {
                _log.LogWarning("[ErrorBoundary] Error item routed to error branch in {CorrId}", ctx.CorrelationId);
                errors.Add(item);
            }
            else
            {
                success.Add(item);
            }
        }

        _log.LogInformation("[ErrorBoundaryNode] Success={S}, Error={E}", success.Count, errors.Count);
        return ValueTask.FromResult(NodeResult.Ok(new List<IReadOnlyList<ExecutionItem>> { success, errors }));
    }
}
