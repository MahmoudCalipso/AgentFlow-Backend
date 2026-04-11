using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AgentFlow.Backend.Core.Nodes;
using AgentFlow.Backend.Core.Execution;
using Microsoft.Extensions.Logging;

namespace AgentFlow.Backend.Nodes.Logic;

public sealed class SwitchNode : INodeHandler
{
    private readonly ILogger<SwitchNode> _log;
    public string NodeId { get; }

    public SwitchNode(string nodeId, ILogger<SwitchNode> log)
    {
        NodeId = nodeId;
        _log = log;
    }

    public ValueTask<NodeResult> HandleAsync(NodeContext ctx, CancellationToken ct)
    {
        var casesRaw = ctx.GetState<List<SwitchCase>>("switch:cases");
        if (casesRaw == null || casesRaw.Count == 0)
            return ValueTask.FromResult(NodeResult.Failure("SwitchNode requires 'switch:cases' state set before execution. Use SetState()."));

        var outputs = new List<IReadOnlyList<ExecutionItem>>();

        foreach (var switchCase in casesRaw)
        {
            var itemsForBranch = new List<ExecutionItem>();
            foreach (var item in ctx.InputItems)
            {
                if (EvaluateCase(switchCase, item.Data))
                    itemsForBranch.Add(item);
            }
            outputs.Add(itemsForBranch);
        }

        // Fallback: items that matched no case
        var allMatched = outputs.SelectMany(x => x).ToHashSet();
        var unmatched  = ctx.InputItems.Where(i => !allMatched.Contains(i)).ToList();
        outputs.Add(unmatched);

        _log.LogInformation("[SwitchNode] Routed {Items} items into {Branches} branches", ctx.InputItems.Count, outputs.Count);
        return ValueTask.FromResult(NodeResult.Ok(outputs));
    }

    private static bool EvaluateCase(SwitchCase c, IDictionary<string, object?> data)
    {
        if (!data.TryGetValue(c.Field, out var val)) return false;
        return string.Equals(val?.ToString(), c.Value, StringComparison.OrdinalIgnoreCase);
    }
}

public sealed record SwitchCase(string Field, string Value);
