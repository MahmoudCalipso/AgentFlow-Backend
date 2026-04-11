using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AgentFlow.Backend.Core.Execution;
using AgentFlow.Backend.Core.Nodes;
using AgentFlow.Backend.Core.Policy;
using AgentFlow.Backend.Core.Observability;
using Microsoft.Extensions.Logging;

namespace AgentFlow.Backend.Nodes.Data;

public sealed class StreamJsonNode : BaseNode
{
    public StreamJsonNode(string nodeId, ILogger<BaseNode> log, IExecutionPolicy policy, IAuditLogger audit)
        : base(nodeId, log, policy, audit)
    {
    }

    public override async ValueTask<IReadOnlyList<IReadOnlyList<ExecutionItem>>> ExecuteAsync(NodeContext ctx, CancellationToken ct)
    {
        var outputItems = new List<ExecutionItem>();

        foreach (var item in ctx.InputItems)
        {
            // If data is a string/binary representing JSON, parse it as a stream of objects
            // For now, handling basic object conversion
            var json = JsonSerializer.Serialize(item.Data);
            outputItems.Add(new ExecutionItem(new Dictionary<string, object?> { ["json"] = json }, PairedItem: item));
        }

        return new List<List<ExecutionItem>> { outputItems };
    }
}
