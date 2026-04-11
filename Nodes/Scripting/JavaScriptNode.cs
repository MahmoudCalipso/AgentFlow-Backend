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
using Jint;
using Jint.Native;

namespace AgentFlow.Backend.Nodes.Scripting;

public sealed class JavaScriptNode : BaseNode
{
    public JavaScriptNode(string nodeId, ILogger<BaseNode> log, IExecutionPolicy policy, IAuditLogger audit)
        : base(nodeId, log, policy, audit)
    {
    }

    public override async ValueTask<IReadOnlyList<IReadOnlyList<ExecutionItem>>> ExecuteAsync(NodeContext ctx, CancellationToken ct)
    {
        var script = ctx.GetConfig<string>(NodeId, "script", "return $json;");
        var outputItems = new List<ExecutionItem>();

        using var engine = new Engine(options => {
            options.LimitMemory(10 * 1024 * 1024); // 10MB limit
            options.TimeoutInterval(TimeSpan.FromSeconds(5));
        });

        foreach (var item in ctx.InputItems)
        {
            // Inject globals matching n8n logic
            engine.SetValue("$json", item.Data);
            engine.SetValue("$node", new { id = NodeId });
            engine.SetValue("$vars", ctx.GetConfig<Dictionary<string, object>>(NodeId, "vars", new()));

            try
            {
                var result = engine.Evaluate(script);
                
                // Parse result back to ExecutionItem data
                var data = ConvertJsResult(result);
                outputItems.Add(new ExecutionItem(data, PairedItem: item));
            }
            catch (Exception ex)
            {
                Log.LogError(ex, "JS execution failed for node {NodeId}", NodeId);
                throw;
            }
        }

        return new List<List<ExecutionItem>> { outputItems };
    }

    private IDictionary<string, object?> ConvertJsResult(JsValue result)
    {
        if (result.IsObject())
        {
            var obj = result.AsObject();
            var dict = new Dictionary<string, object?>();
            foreach (var prop in obj.GetOwnProperties())
            {
                dict[prop.Key.AsString()] = prop.Value.Value.ToObject();
            }
            return dict;
        }
        
        return new Dictionary<string, object?> { ["output"] = result.ToObject() };
    }
}
