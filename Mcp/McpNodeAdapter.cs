using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AgentFlow.Backend.Core.Execution;
using AgentFlow.Backend.Core.Nodes;
using AgentFlow.Backend.Core.Reliability;
using Microsoft.Extensions.Logging;

namespace AgentFlow.Backend.Mcp;

public sealed class McpNodeAdapter : BaseNode
{
    private readonly IMcpClient _mcp;
    private readonly string _toolName;

    public McpNodeAdapter(string nodeId, string toolName, IMcpClient mcp, ILogger<BaseNode> log, IExecutionPolicy pol, IAuditLogger audit)
        : base(nodeId, log, pol, audit) 
    { 
        _mcp = mcp; 
        _toolName = toolName; 
    }

    public override async ValueTask<IReadOnlyList<IReadOnlyList<ExecutionItem>>> ExecuteAsync(NodeContext ctx, CancellationToken ct)
    {
        var outputItems = new List<ExecutionItem>();
        foreach (var item in ctx.InputItems)
        {
            var res = await _mcp.CallToolAsync(_toolName, item.Data, ct);
            
            // Extract text from content
            var text = string.Join("\n", res.Content.Select(c => c.Text ?? ""));
            var data = new Dictionary<string, object?> { ["output"] = text, ["isError"] = res.IsError };
            
            outputItems.Add(new ExecutionItem(data, PairedItem: item));
        }
        return new List<List<ExecutionItem>> { outputItems };
    }
}