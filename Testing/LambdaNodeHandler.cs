using System;
using System.Threading;
using System.Threading.Tasks;
using AgentFlow.Backend.Core.Execution;
using AgentFlow.Backend.Core.Nodes;

namespace AgentFlow.Backend.Testing;

internal sealed class LambdaNodeHandler : INodeHandler
{
    public string NodeId { get; }
    private readonly Func<NodeContext, CancellationToken, ValueTask<NodeResult>> _handler;

    public LambdaNodeHandler(string nodeId, Func<NodeContext, CancellationToken, ValueTask<NodeResult>> handler)
    {
        NodeId = nodeId;
        _handler = handler;
    }

    public ValueTask<NodeResult> HandleAsync(NodeContext ctx, CancellationToken ct)
        => _handler(ctx, ct);
}
