using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AgentFlow.Backend.Core.Execution;
using AgentFlow.Backend.Core.Nodes;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

namespace AgentFlow.Backend.Api;

[ApiController]
[Route("api/[controller]")]
public sealed class ValidationController : ControllerBase
{
    private readonly IServiceProvider _sp;

    public ValidationController(IServiceProvider sp)
    {
        _sp = sp;
    }

    [HttpPost("validate")]
    public IActionResult ValidateGraph([FromBody] GraphValidationRequest request)
    {
        var errors = new List<string>();

        foreach (var node in request.Nodes)
        {
            var handler = _sp.GetKeyedService<INodeHandler>(node.Type);
            if (handler == null)
            {
                errors.Add($"Node {node.Id}: Unsupported type '{node.Type}'.");
            }
        }

        // Circular dependency check
        if (HasCircularDependency(request))
        {
            errors.Add("Graph contains circular dependencies which are not allowed in this mode.");
        }

        if (errors.Any()) return BadRequest(new { valid = false, errors });
        return Ok(new { valid = true });
    }

    private bool HasCircularDependency(GraphValidationRequest request)
    {
        var visited = new HashSet<string>();
        var stack = new HashSet<string>();

        foreach (var node in request.Nodes)
        {
            if (CheckCycle(node.Id, request.Connections, visited, stack)) return true;
        }

        return false;
    }

    private bool CheckCycle(string nodeId, List<ConnectionRequest> conns, HashSet<string> visited, HashSet<string> stack)
    {
        if (stack.Contains(nodeId)) return true;
        if (visited.Contains(nodeId)) return false;

        visited.Add(nodeId);
        stack.Add(nodeId);

        var neighbors = conns.Where(c => c.SourceNodeId == nodeId).Select(c => c.TargetNodeId);
        foreach (var neighbor in neighbors)
        {
            if (CheckCycle(neighbor, conns, visited, stack)) return true;
        }

        stack.Remove(nodeId);
        return false;
    }
}

public sealed record GraphValidationRequest(List<NodeRequest> Nodes, List<ConnectionRequest> Connections);
