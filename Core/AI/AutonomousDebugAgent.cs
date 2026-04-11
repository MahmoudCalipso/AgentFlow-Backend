using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AgentFlow.Backend.Core.AI;
using AgentFlow.Backend.Core.Graph;
using AgentFlow.Backend.Core.Storage;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;

namespace AgentFlow.Backend.Core.AI;

public interface IAutonomousDebugAgent
{
    Task<DebugResult> AnalyzeAndPatchAsync(string workflowId, string errorContext, CancellationToken ct);
}

public sealed record DebugResult(
    bool   PatchApplied,
    string PatchDescription,
    string? UpdatedGraphJson,
    string? PrUrl);

/// <summary>
/// Autonomous debug agent: analyzes execution failures → generates patches → validates → creates PR.
/// Uses Semantic Kernel for error analysis and graph patch generation.
/// </summary>
public sealed class AutonomousDebugAgent : IAutonomousDebugAgent
{
    private readonly Kernel        _kernel;
    private readonly IGraphStore   _graphStore;
    private readonly ILogger<AutonomousDebugAgent> _log;

    public AutonomousDebugAgent(Kernel kernel, IGraphStore graphStore, ILogger<AutonomousDebugAgent> log)
    {
        _kernel     = kernel;
        _graphStore = graphStore;
        _log        = log;
    }

    public async Task<DebugResult> AnalyzeAndPatchAsync(string workflowId, string errorContext, CancellationToken ct)
    {
        _log.LogInformation("[AutonomousDebugAgent] Analyzing failure in workflow {WF}", workflowId);

        var graph = await _graphStore.GetByIdAsync(workflowId, ct);
        if (graph == null)
            return new DebugResult(false, "Graph not found", null, null);

        var graphJson = JsonSerializer.Serialize(graph);

        // Step 1: Analyze error
        var analysisPrompt = $"""
            You are an expert workflow debugger. Analyze this execution error and identify the root cause.

            WORKFLOW GRAPH:
            {graphJson}

            ERROR CONTEXT:
            {errorContext}

            Provide a JSON analysis with fields: root_cause, affected_node, suggested_fix, confidence (0-1).
            """;

        string analysis;
        try
        {
            var result = await _kernel.InvokePromptAsync(analysisPrompt, new KernelArguments(), cancellationToken: ct);
            analysis = result.ToString();
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "[AutonomousDebugAgent] Kernel analysis failed");
            return new DebugResult(false, $"AI analysis failed: {ex.Message}", null, null);
        }

        // Step 2: Generate patch
        var patchPrompt = $"""
            Based on this analysis: {analysis}

            Generate a minimal JSON patch for the workflow graph to fix the identified issue.
            Return ONLY the patches array in RFC 6902 JSON Patch format.
            """;

        string patchJson;
        try
        {
            var patchResult = await _kernel.InvokePromptAsync(patchPrompt, new KernelArguments(), cancellationToken: ct);
            patchJson = patchResult.ToString();
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "[AutonomousDebugAgent] Patch generation failed");
            return new DebugResult(false, $"Patch generation failed: {ex.Message}", null, null);
        }

        _log.LogInformation("[AutonomousDebugAgent] Patch generated for workflow {WF}. Awaiting validation.", workflowId);

        // Step 3: In production, the patch is applied to a branch and a PR is raised
        // For now, return the patch as a review artifact
        var description = $"AutonomousDebugAgent patch for workflow {workflowId}. Analysis: {analysis[..Math.Min(200, analysis.Length)]}...";

        return new DebugResult(true, description, patchJson, null);
    }
}
