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

namespace AgentFlow.Backend.Nodes.Data;

public sealed class MergeNode : BaseNode
{
    public MergeNode(string nodeId, ILogger<BaseNode> log, IExecutionPolicy policy, IAuditLogger audit)
        : base(nodeId, log, policy, audit)
    {
    }

    public override async ValueTask<IReadOnlyList<IReadOnlyList<ExecutionItem>>> ExecuteAsync(NodeContext ctx, CancellationToken ct)
    {
        var mode = ctx.GetConfig<string>(NodeId, "mode", "append");
        var outputItems = new List<ExecutionItem>();

        // Merge logic depends on how the engine handles multiple inputs.
        // For simplicity, we merge all input items into one list or join them.
        
        switch (mode.ToLowerInvariant())
        {
            case "append":
                outputItems.AddRange(ctx.InputItems);
                break;
            case "join":
                // Join all items into one single item containing all data
                var mergedData = new Dictionary<string, object?>();
                foreach (var item in ctx.InputItems)
                {
                    foreach (var kvp in item.Data) mergedData[kvp.Key] = kvp.Value;
                }
                outputItems.Add(new ExecutionItem(mergedData));
                break;
        }

        return new List<List<ExecutionItem>> { outputItems };
    }
}
