using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AgentFlow.Backend.Core.Execution;
using AgentFlow.Backend.Core.Nodes;
using AgentFlow.Backend.Core.Policy;
using AgentFlow.Backend.Core.Observability;
using Microsoft.Extensions.Logging;
using Jint;

namespace AgentFlow.Backend.Nodes.Logic;

public sealed class ConditionNode : BaseNode
{
    public ConditionNode(string nodeId, ILogger<BaseNode> log, IExecutionPolicy policy, IAuditLogger audit)
        : base(nodeId, log, policy, audit)
    {
    }

    public override async ValueTask<IReadOnlyList<IReadOnlyList<ExecutionItem>>> ExecuteAsync(NodeContext ctx, CancellationToken ct)
    {
        var expression = ctx.GetConfig<string>(NodeId, "expression", "true");
        var trueItems = new List<ExecutionItem>();
        var falseItems = new List<ExecutionItem>();

        using var engine = new Engine(options => {
            options.TimeoutInterval(TimeSpan.FromSeconds(1));
        });

        foreach (var item in ctx.InputItems)
        {
            try
            {
                foreach (var kvp in item.Data) engine.SetValue(kvp.Key, kvp.Value);
                
                var result = engine.Evaluate(expression);
                if (result.IsBoolean() && result.AsBoolean()) trueItems.Add(item);
                else falseItems.Add(item);
            }
            catch (Exception ex)
            {
                Log.LogError(ex, "Condition evaluation failed for node {NodeId}", NodeId);
                falseItems.Add(item);
            }
        }

        return new List<List<ExecutionItem>> { trueItems, falseItems };
    }
}