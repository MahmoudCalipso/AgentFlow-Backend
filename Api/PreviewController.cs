using System;
using System.Threading;
using System.Threading.Tasks;
using AgentFlow.Backend.Core.Graph;
using AgentFlow.Backend.Core.Observability;
using AgentFlow.Backend.Core.Storage;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace AgentFlow.Backend.Api;

[ApiController]
[Route("api/v1/preview")]
public sealed class PreviewController : ControllerBase
{
    private readonly IGraphStore      _graphStore;
    private readonly IGraphValidator  _graphValidator;
    private readonly ExecutionCostTracker _costTracker;
    private readonly ILogger<PreviewController> _log;

    public PreviewController(
        IGraphStore graphStore,
        IGraphValidator graphValidator,
        ExecutionCostTracker costTracker,
        ILogger<PreviewController> log)
    {
        _graphStore     = graphStore;
        _graphValidator = graphValidator;
        _costTracker    = costTracker;
        _log            = log;
    }

    /// <summary>
    /// Returns estimated cost, latency, and success probability for a workflow before execution.
    /// </summary>
    [HttpGet("{workflowId}")]
    public async Task<IActionResult> PreviewAsync(string workflowId, CancellationToken ct)
    {
        var graph = await _graphStore.GetByIdAsync(workflowId, ct);
        if (graph == null) return NotFound(new { error = $"Workflow '{workflowId}' not found" });

        // Use async IGraphValidator
        var validationResult = await _graphValidator.ValidateAsync(graph, ct);
        var nodeCount        = graph.Nodes.Count;
        var edgeCount        = graph.Edges.Count;

        var aiNodeCount = graph.Nodes.Count(n =>
            n.Type.Contains("ai",    StringComparison.OrdinalIgnoreCase) ||
            n.Type.Contains("llm",   StringComparison.OrdinalIgnoreCase) ||
            n.Type.Contains("agent", StringComparison.OrdinalIgnoreCase));

        var estimatedLatencyMs = nodeCount * 120L;
        var estimatedCostUsd   = nodeCount * 0.0002 + aiNodeCount * 0.01;
        var successProb        = validationResult.IsValid ? Math.Clamp(0.92 - aiNodeCount * 0.02, 0.1, 0.99) : 0.3;

        _log.LogInformation("[PreviewController] Preview for {WF}: valid={V}, est_cost=${Cost:F4}", workflowId, validationResult.IsValid, estimatedCostUsd);

        return Ok(new
        {
            workflow_id          = workflowId,
            valid                = validationResult.IsValid,
            validation_errors    = validationResult.Errors,
            node_count           = nodeCount,
            edge_count           = edgeCount,
            ai_node_count        = aiNodeCount,
            estimated_latency_ms = estimatedLatencyMs,
            estimated_cost_usd   = Math.Round(estimatedCostUsd, 6),
            success_probability  = Math.Round(successProb, 3),
            generated_at         = DateTimeOffset.UtcNow
        });
    }
}
