using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace AgentFlow.Backend.Core.Graph;

public interface IGraphValidator
{
    Task<ValidationResult> ValidateAsync(GraphDefinition definition, CancellationToken ct);
}



public sealed class GraphValidator : IGraphValidator
{
    private static readonly HashSet<string> _knownTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "webhook-trigger", "http-request", "condition", "mcp-tool", "wasm", "agentic", "merge", "split", "transform"
    };

    private readonly ILogger<GraphValidator> _log;

    public GraphValidator(ILogger<GraphValidator> log)
    {
        _log = log;
    }

    public Task<ValidationResult> ValidateAsync(GraphDefinition definition, CancellationToken ct)
    {
        var errors = new List<string>();
        var warnings = new List<string>();

        if (string.IsNullOrWhiteSpace(definition.Id))
            errors.Add("Graph ID is required.");

        if (definition.Nodes.Count == 0)
            errors.Add("Graph must have at least one node.");

        var nodeIds = new HashSet<string>();
        foreach (var node in definition.Nodes)
        {
            if (string.IsNullOrWhiteSpace(node.Id))
            {
                errors.Add("All nodes must have a non-empty ID.");
                continue;
            }
            if (!nodeIds.Add(node.Id))
                errors.Add($"Duplicate node ID: {node.Id}");

            if (!_knownTypes.Contains(node.Type))
                warnings.Add($"Unknown node type '{node.Type}' for node '{node.Id}'. It may not have a registered handler.");
        }

        foreach (var edge in definition.Edges)
        {
            if (!nodeIds.Contains(edge.SourceNodeId))
                errors.Add($"Edge references non-existent source node '{edge.SourceNodeId}'.");
            if (!nodeIds.Contains(edge.TargetNodeId))
                errors.Add($"Edge references non-existent target node '{edge.TargetNodeId}'.");
        }

        if (HasCycle(definition.Nodes, definition.Edges))
        {
            warnings.Add("Graph contains a cycle. Ensure this is intentional (e.g. retry loops).");
        }

        var entryNodes = FindEntryNodes(definition.Nodes, definition.Edges);
        if (entryNodes.Count == 0)
            errors.Add("Graph has no entry nodes (nodes with no incoming edges).");

        _log.LogDebug("Graph {Id} validated: {ErrorCount} errors, {WarnCount} warnings", definition.Id, errors.Count, warnings.Count);
        return Task.FromResult(new ValidationResult(errors.Count == 0, errors, warnings));
    }

    private static bool HasCycle(IReadOnlyList<NodeDef> nodes, IReadOnlyList<EdgeDef> edges)
    {
        var adjacency = new Dictionary<string, List<string>>();
        foreach (var node in nodes)
            adjacency[node.Id] = new List<string>();

        foreach (var edge in edges)
        {
            if (adjacency.ContainsKey(edge.SourceNodeId))
                adjacency[edge.SourceNodeId].Add(edge.TargetNodeId);
        }

        var visited = new HashSet<string>();
        var inStack = new HashSet<string>();

        bool Dfs(string nodeId)
        {
            if (inStack.Contains(nodeId)) return true;
            if (visited.Contains(nodeId)) return false;

            visited.Add(nodeId);
            inStack.Add(nodeId);

            if (adjacency.TryGetValue(nodeId, out var neighbors))
            {
                foreach (var neighbor in neighbors)
                {
                    if (Dfs(neighbor)) return true;
                }
            }

            inStack.Remove(nodeId);
            return false;
        }

        return nodes.Any(n => !visited.Contains(n.Id) && Dfs(n.Id));
    }

    private static List<string> FindEntryNodes(IReadOnlyList<NodeDef> nodes, IReadOnlyList<EdgeDef> edges)
    {
        var hasIncoming = edges.Select(e => e.TargetNodeId).ToHashSet();
        return nodes.Where(n => !hasIncoming.Contains(n.Id)).Select(n => n.Id).ToList();
    }
}
