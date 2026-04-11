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

public sealed class ScheduleTriggerNode : BaseNode
{
    public ScheduleTriggerNode(string nodeId, ILogger<BaseNode> log, IExecutionPolicy policy, IAuditLogger audit)
        : base(nodeId, log, policy, audit)
    {
    }

    public override ValueTask<IReadOnlyList<IReadOnlyList<ExecutionItem>>> ExecuteAsync(NodeContext ctx, CancellationToken ct)
    {
        // Simple passthrough for the trigger event
        return new ValueTask<IReadOnlyList<IReadOnlyList<ExecutionItem>>>(new List<List<ExecutionItem>> { ctx.InputItems.ToList() });
    }
}
