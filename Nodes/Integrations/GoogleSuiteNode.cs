using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AgentFlow.Backend.Core.Execution;
using AgentFlow.Backend.Core.Nodes;
using AgentFlow.Backend.Core.Policy;
using AgentFlow.Backend.Core.Observability;
using Microsoft.Extensions.Logging;

namespace AgentFlow.Backend.Nodes.Integrations;

public sealed class GoogleSuiteNode : BaseNode
{
    public GoogleSuiteNode(string nodeId, ILogger<BaseNode> log, IExecutionPolicy policy, IAuditLogger audit)
        : base(nodeId, log, policy, audit)
    {
    }

    public override async ValueTask<IReadOnlyList<IReadOnlyList<ExecutionItem>>> ExecuteAsync(NodeContext ctx, CancellationToken ct)
    {
        var service = ctx.GetConfig<string>(NodeId, "service", "sheets"); // sheets, gmail, drive
        var action = ctx.GetConfig<string>(NodeId, "action", "read");
        var outputItems = new List<ExecutionItem>();

        // For n8n parity, this node would typically use Google.Apis.* 
        // Here we implement the architecture to support it, with a mock success/log for now.
        
        Log.LogInformation("Executing Google {Service} action: {Action}", service, action);

        foreach (var item in ctx.InputItems)
        {
            outputItems.Add(new ExecutionItem(new Dictionary<string, object?> { 
                ["service"] = service,
                ["action"] = action,
                ["status"] = "success",
                ["timestamp"] = DateTime.UtcNow
            }, PairedItem: item));
        }

        return new List<List<ExecutionItem>> { outputItems };
    }
}
