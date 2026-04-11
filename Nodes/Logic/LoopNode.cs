using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AgentFlow.Backend.Core.Execution;
using AgentFlow.Backend.Core.Nodes;
using AgentFlow.Backend.Core.Policy;
using AgentFlow.Backend.Core.Observability;
using Microsoft.Extensions.Logging;

namespace AgentFlow.Backend.Nodes.Logic;

public sealed class LoopNode : BaseNode
{
    public LoopNode(string nodeId, ILogger<BaseNode> log, IExecutionPolicy policy, IAuditLogger audit)
        : base(nodeId, log, policy, audit)
    {
    }

    public override async ValueTask<IReadOnlyList<IReadOnlyList<ExecutionItem>>> ExecuteAsync(NodeContext ctx, CancellationToken ct)
    {
        var itemsToLoop = ctx.GetConfig<List<object>>(NodeId, "items", new());
        var outputs = new List<ExecutionItem>();

        foreach (var raw in itemsToLoop)
        {
            var data = raw switch
            {
                IDictionary<string, object?> dict => dict,
                _ => new Dictionary<string, object?> { ["value"] = raw }
            };
            
            outputs.Add(new ExecutionItem(data as Dictionary<string, object?> ?? new()));
        }

        // Loop node typically has two outputs: "item" (for iteration) and "done" (when empty)
        // This is a simplified n8n style implementation
        return new List<List<ExecutionItem>> { outputs, new List<ExecutionItem>() }; 
    }
}
