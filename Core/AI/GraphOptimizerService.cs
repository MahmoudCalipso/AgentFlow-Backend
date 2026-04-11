using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AgentFlow.Backend.Core.Graph;
using AgentFlow.Backend.Core.Storage;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;

namespace AgentFlow.Backend.Core.AI;

public interface IGraphOptimizerService
{
    Task<OptimizationReport> OptimizeAsync(string workflowId, CancellationToken ct);
    Task<GraphDefinition> ApplyOptimizationsAsync(GraphDefinition graph, IReadOnlyList<OptimizationHint> hints, CancellationToken ct);
}

public sealed record OptimizationReport(
    string WorkflowId,
    IReadOnlyList<OptimizationHint> Hints,
    double EstimatedSpeedupPercent,
    double EstimatedCostReductionPercent);

/// <summary>
/// AI-powered graph optimizer. Analyzes execution traces to suggest optimizations.
/// </summary>
public sealed class GraphOptimizerService : IGraphOptimizerService
{
    private readonly Kernel      _kernel;
    private readonly IGraphStore _graphStore;
    private readonly ILogger<GraphOptimizerService> _log;

    public GraphOptimizerService(Kernel kernel, IGraphStore graphStore, ILogger<GraphOptimizerService> log)
    {
        _kernel     = kernel;
        _graphStore = graphStore;
        _log        = log;
    }

    public async Task<OptimizationReport> OptimizeAsync(string workflowId, CancellationToken ct)
    {
        var graph = await _graphStore.GetByIdAsync(workflowId, ct);
        if (graph == null) throw new InvalidOperationException($"Workflow {workflowId} not found.");

        var graphJson = JsonSerializer.Serialize(graph);
        var prompt    = $"""
            You are an expert workflow optimizer. Analyze this workflow graph and suggest optimizations.

            GRAPH:
            {graphJson}

            Return a JSON array of optimization hints, each with:
            - type: "parallelize" | "cache" | "eliminate" | "reroute"
            - node_id: affected node
            - description: what to do
            - estimated_speedup_percent: number
            - estimated_cost_reduction_percent: number
            """;

        var hints = new List<OptimizationHint>();
        double speedup = 0, costReduction = 0;

        try
        {
            var result = await _kernel.InvokePromptAsync(prompt, new KernelArguments(), cancellationToken: ct);
            var json   = result.ToString().Trim();

            if (json.Contains("```"))
            {
                var start = json.IndexOf('[');
                var end   = json.LastIndexOf(']');
                if (start >= 0 && end > start) json = json[start..(end + 1)];
            }

            var parsed = JsonSerializer.Deserialize<List<JsonElement>>(json);
            if (parsed != null)
            {
                foreach (var el in parsed)
                {
                    var type   = el.TryGetProperty("type", out var t) ? t.GetString() ?? "unknown" : "unknown";
                    var nodeId = el.TryGetProperty("node_id", out var nid) ? nid.GetString() ?? "" : "";
                    var desc   = el.TryGetProperty("description", out var d)  ? d.GetString()  ?? "" : "";
                    var sp     = el.TryGetProperty("estimated_speedup_percent", out var spe) ? spe.GetDouble() : 0;
                    var cr     = el.TryGetProperty("estimated_cost_reduction_percent", out var cre) ? cre.GetDouble() : 0;

                    // OptimizationHint: (HintType, Description, AffectedNodeId, PotentialImpactPercent)
                    var impact = (float)Math.Max(sp, cr);
                    hints.Add(new OptimizationHint(type, desc, nodeId, impact));
                    
                    speedup       = Math.Max(speedup, sp);
                    costReduction = Math.Max(costReduction, cr);
                }
            }
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "[GraphOptimizer] AI analysis failed for workflow {WF}", workflowId);
        }

        _log.LogInformation("[GraphOptimizer] {Count} hints for {WF}: ~{Sp:F0}% faster, ~{Cr:F0}% cheaper",
            hints.Count, workflowId, speedup, costReduction);

        return new OptimizationReport(workflowId, hints, speedup, costReduction);
    }

    public Task<GraphDefinition> ApplyOptimizationsAsync(GraphDefinition graph, IReadOnlyList<OptimizationHint> hints, CancellationToken ct)
    {
        var eliminated = hints.Where(h => string.Equals(h.HintType, "eliminate", StringComparison.OrdinalIgnoreCase))
                              .Select(h => h.AffectedNodeId)
                              .ToHashSet();
                              
        if (eliminated.Count == 0) return Task.FromResult(graph);

        var newNodes = graph.Nodes.Where(n => !eliminated.Contains(n.Id)).ToList();
        var newEdges = graph.Edges.Where(e => !eliminated.Contains(e.SourceNodeId) && !eliminated.Contains(e.TargetNodeId)).ToList();
        var optimized = graph with { Nodes = newNodes, Edges = newEdges };

        _log.LogInformation("[GraphOptimizer] Applied {Count} eliminations to graph {Id}", eliminated.Count, graph.Id);
        return Task.FromResult(optimized);
    }
}
