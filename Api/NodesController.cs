using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AgentFlow.Backend.Core.Nodes;
using AgentFlow.Backend.Mcp;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

namespace AgentFlow.Backend.Api;

[ApiController]
[Route("api/v1/[controller]")]
public sealed class NodesController : ControllerBase
{
    private readonly NodeDiscoveryService _discovery;

    public NodesController(NodeDiscoveryService discovery)
    {
        _discovery = discovery;
    }

    [HttpGet]
    public IActionResult ListNodes()
    {
        var nodes = new List<object>();

        // Add discovered nodes (Native + Dynamic MCP)
        foreach (var node in _discovery.GetAllNodes())
        {
            nodes.Add(new
            {
                id = node.Id,
                type = node.Type,
                name = node.Name,
                category = GetCategoryForType(node.Type),
                description = node switch {
                    McpNodeAdapter => "Dynamic Community Node",
                    _ => "Native AgentFlow primitive"
                }
            });
        }
        
        return Ok(nodes);
    }

    private string GetCategoryForType(string type)
    {
        if (type.Contains("trigger")) return "Triggers";
        if (type.Contains("condition") || type.Contains("loop")) return "Logic";
        if (type.Contains("stream") || type.Contains("merge")) return "Data";
        if (type.Contains("wasm") || type.Contains("code")) return "Scripting";
        if (type.Contains("agent") || type.Contains("ai")) return "AI";
        return "General";
    }
}
