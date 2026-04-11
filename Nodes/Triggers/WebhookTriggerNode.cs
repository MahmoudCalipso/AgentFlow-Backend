using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AgentFlow.Backend.Core.Execution;
using AgentFlow.Backend.Core.Nodes;
using AgentFlow.Backend.Core.Policy;
using AgentFlow.Backend.Core.Observability;
using Microsoft.Extensions.Logging;

namespace AgentFlow.Backend.Nodes.Triggers;

public sealed class WebhookTriggerNode : BaseNode
{
    public WebhookTriggerNode(string nodeId, ILogger<BaseNode> log, IExecutionPolicy policy, IAuditLogger audit)
        : base(nodeId, log, policy, audit)
    {
    }

    public override ValueTask<IReadOnlyList<IReadOnlyList<ExecutionItem>>> ExecuteAsync(NodeContext ctx, CancellationToken ct)
    {
        // For a trigger node, the "execution" is usually the first step receiving external data.
        // It simply passes the input items (which were injected by the Ingress) to the output.
        return new ValueTask<IReadOnlyList<IReadOnlyList<ExecutionItem>>>(new List<List<ExecutionItem>> { ctx.InputItems.ToList() });
    }
}