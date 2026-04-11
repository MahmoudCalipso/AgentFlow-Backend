using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AgentFlow.Backend.Core.Serialization;
using AgentFlow.Backend.Core.Execution;
using AgentFlow.Backend.Core.Nodes;
using AgentFlow.Backend.Core.Policy;
using AgentFlow.Backend.Core.Observability;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;

namespace AgentFlow.Backend.Nodes.Integrations;

public sealed class AgentAiNode : BaseNode
{
    private readonly IServiceProvider _sp;

    public AgentAiNode(string nodeId, IServiceProvider sp, ILogger<BaseNode> log, IExecutionPolicy policy, IAuditLogger audit)
        : base(nodeId, log, policy, audit)
    {
        _sp = sp;
    }

    public override async ValueTask<IReadOnlyList<IReadOnlyList<ExecutionItem>>> ExecuteAsync(NodeContext ctx, CancellationToken ct)
    {
        var model = ctx.GetConfig<string>(NodeId, "model", "gpt-4o");
        var systemPrompt = ctx.GetConfig<string>(NodeId, "systemPrompt", "You are an AI agent. Plan and execute the task.");
        var outputItems = new List<ExecutionItem>();

        var kernel = _sp.GetRequiredService<Kernel>();
        
        foreach (var item in ctx.InputItems)
        {
            var userPrompt = JsonSerializer.Serialize(item.Data, AgentFlowJsonContext.Default.IDictionaryStringObject);
            
            var result = await kernel.InvokePromptAsync($"{systemPrompt}\n\nTask: {userPrompt}", cancellationToken: ct);
            
            outputItems.Add(new ExecutionItem(new Dictionary<string, object?> { ["response"] = result.ToString() }, PairedItem: item));
        }

        return new List<List<ExecutionItem>> { outputItems };
    }
}
