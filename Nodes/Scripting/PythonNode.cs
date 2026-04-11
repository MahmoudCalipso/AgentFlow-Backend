using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AgentFlow.Backend.Core.Execution;
using AgentFlow.Backend.Core.Nodes;
using AgentFlow.Backend.Core.Policy;
using AgentFlow.Backend.Core.Observability;
using Microsoft.Extensions.Logging;
using IronPython.Hosting;
using Microsoft.Scripting.Hosting;

namespace AgentFlow.Backend.Nodes.Scripting;

public sealed class PythonNode : BaseNode
{
    private static readonly ScriptEngine _engine = Python.CreateEngine();

    public PythonNode(string nodeId, ILogger<BaseNode> log, IExecutionPolicy policy, IAuditLogger audit)
        : base(nodeId, log, policy, audit)
    {
    }

    public override async ValueTask<IReadOnlyList<IReadOnlyList<ExecutionItem>>> ExecuteAsync(NodeContext ctx, CancellationToken ct)
    {
        var script = ctx.GetConfig<string>(NodeId, "script", "return $json");
        var outputItems = new List<ExecutionItem>();

        foreach (var item in ctx.InputItems)
        {
            var scope = _engine.CreateScope();
            scope.SetVariable("json", item.Data);
            scope.SetVariable("node", new { id = NodeId });
            scope.SetVariable("vars", ctx.GetConfig<Dictionary<string, object>>(NodeId, "vars", new()));

            try
            {
                // Wrap script as a function for easier return value handling if needed
                // For now, assume it modifies 'json' or sets 'result'
                var source = _engine.CreateScriptSourceFromString(script);
                var result = source.Execute(scope);

                var finalData = scope.GetVariable("json") as IDictionary<string, object?> 
                               ?? new Dictionary<string, object?> { ["output"] = result };
                
                outputItems.Add(new ExecutionItem(finalData, PairedItem: item));
            }
            catch (Exception ex)
            {
                Log.LogError(ex, "Python execution failed for node {NodeId}", NodeId);
                throw;
            }
        }

        return new List<List<ExecutionItem>> { outputItems };
    }
}
