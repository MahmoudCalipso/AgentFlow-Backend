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

public sealed class StreamCsvNode : BaseNode
{
    public StreamCsvNode(string nodeId, ILogger<BaseNode> log, IExecutionPolicy policy, IAuditLogger audit)
        : base(nodeId, log, policy, audit)
    {
    }

    public override async ValueTask<IReadOnlyList<IReadOnlyList<ExecutionItem>>> ExecuteAsync(NodeContext ctx, CancellationToken ct)
    {
        var outputItems = new List<ExecutionItem>();

        foreach (var item in ctx.InputItems)
        {
            // Simplified CSV generation from data
            var keys = item.Data.Keys.ToList();
            var csv = string.Join(",", keys) + "\n" + string.Join(",", keys.Select(k => item.Data[k]?.ToString() ?? ""));
            outputItems.Add(new ExecutionItem(new Dictionary<string, object?> { ["csv"] = csv }, PairedItem: item));
        }

        return new List<List<ExecutionItem>> { outputItems };
    }
}
