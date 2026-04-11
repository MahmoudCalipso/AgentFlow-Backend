using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AgentFlow.Backend.Core.Graph;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using AgentFlow.Backend.Core.Serialization;

namespace AgentFlow.Backend.Core.AI;

public sealed class AiCopilotService : IAiCopilotService
{
    private readonly Kernel _kernel;
    private readonly ILogger<AiCopilotService> _log;

    private static readonly string NodeTypeCatalog =
        "Available node types:\n" +
        "- webhook-trigger: Entry point for HTTP POST data\n" +
        "- http-request: Outbound HTTP GET/POST with auth\n" +
        "- condition: Boolean routing (2 output ports: true/false)\n" +
        "- mcp-tool: Calls a registered MCP tool\n" +
        "- wasm: Executes isolated WebAssembly code\n" +
        "- agentic: Plan-Act-Verify-Reflect AI loop\n" +
        "- merge: Combines items from multiple input ports\n" +
        "- split: Splits one item into many\n" +
        "- transform: Data transformations (rename, compute)";

    public AiCopilotService(Kernel kernel, ILogger<AiCopilotService> log)
    {
        _kernel = kernel;
        _log = log;
    }

    public async Task<CopilotSuggestion> SuggestNextNodeAsync(GraphDefinition graph, string lastNodeId, CancellationToken ct)
    {
        var lastNode = graph.Nodes.FirstOrDefault(n => n.Id == lastNodeId);
        var graphSummary = string.Join("\n", graph.Nodes.Select(n => $"- {n.Id} ({n.Type})"));
        var jsonExample = "{\"nodeType\":\"<type>\",\"reasoning\":\"<why>\",\"confidence\":0.9}";

        var prompt =
            NodeTypeCatalog + "\n\n" +
            "Current workflow graph:\n" + graphSummary + "\n\n" +
            $"The user just added a '{lastNode?.Type ?? "unknown"}' node (id: {lastNodeId}).\n" +
            "What should the NEXT node be? Reply in this JSON format only:\n" +
            jsonExample;

        var result = await _kernel.InvokePromptAsync(prompt, cancellationToken: ct);
        var raw = result.GetValue<string>() ?? "{}";

        try
        {
            using var doc = JsonDocument.Parse(StripFences(raw));
            var root = doc.RootElement;
            return new CopilotSuggestion(
                root.TryGetProperty("nodeType", out var nt) ? nt.GetString() ?? "transform" : "transform",
                root.TryGetProperty("reasoning", out var r) ? r.GetString() ?? "" : "",
                root.TryGetProperty("confidence", out var c) ? c.GetSingle() : 0.5f);
        }
        catch
        {
            _log.LogWarning("Failed to parse copilot suggestion, returning default");
            return new CopilotSuggestion("transform", "Unable to determine suggestion.", 0.5f);
        }
    }

    public async Task<string> ExplainErrorAsync(string nodeType, string errorMessage, string? inputDataJson, CancellationToken ct)
    {
        var inputSection = inputDataJson is not null ? $"Input data:\n{inputDataJson}" : "No input data available.";
        var prompt =
            $"You are an expert workflow debugger for AgentFlow.\n\n" +
            $"A '{nodeType}' node failed with this error:\n{errorMessage}\n\n" +
            $"{inputSection}\n\n" +
            "Explain:\n" +
            "1. What likely caused this error (in plain English)\n" +
            "2. How to fix it (concrete steps)\n" +
            "3. How to prevent it in the future\n\n" +
            "Be concise and developer-friendly.";

        var result = await _kernel.InvokePromptAsync(prompt, cancellationToken: ct);
        return result.GetValue<string>() ?? "Unable to explain the error at this time.";
    }

    public async Task<IReadOnlyList<OptimizationHint>> AnalyzeOptimizationAsync(GraphDefinition graph, CancellationToken ct)
    {
        var nodeList = string.Join("\n", graph.Nodes.Select(n => $"- {n.Id} ({n.Type})"));
        var edgeList = string.Join("\n", graph.Edges.Select(e => $"  {e.SourceNodeId}:{e.SourcePort} -> {e.TargetNodeId}:{e.TargetPort}"));
        var jsonExample = "[{\"hintType\":\"parallelization\",\"description\":\"...\",\"affectedNodeId\":\"nodeX\",\"potentialImpactPercent\":30}]";

        var prompt =
            "Analyze this AgentFlow workflow graph for performance and reliability issues.\n" +
            "Respond with a JSON array of optimization hints.\n\n" +
            $"Nodes:\n{nodeList}\n\nEdges:\n{edgeList}\n\n" +
            $"Reply ONLY with a JSON array in this format:\n{jsonExample}";

        var result = await _kernel.InvokePromptAsync(prompt, cancellationToken: ct);
        var raw = result.GetValue<string>() ?? "[]";

        try
        {
            using var doc = JsonDocument.Parse(StripFences(raw));
            var hints = new List<OptimizationHint>();
            foreach (var item in doc.RootElement.EnumerateArray())
            {
                hints.Add(new OptimizationHint(
                    item.TryGetProperty("hintType", out var ht) ? ht.GetString() ?? "" : "",
                    item.TryGetProperty("description", out var d) ? d.GetString() ?? "" : "",
                    item.TryGetProperty("affectedNodeId", out var an) ? an.GetString() ?? "" : "",
                    item.TryGetProperty("potentialImpactPercent", out var p) ? p.GetSingle() : 0f));
            }
            return hints;
        }
        catch
        {
            _log.LogWarning("Failed to parse optimization hints from copilot");
            return Array.Empty<OptimizationHint>();
        }
    }

    public async Task<string> AutoCompleteConfigAsync(string nodeType, string partialConfig, CancellationToken ct)
    {
        var prompt =
            $"You are an AgentFlow config assistant. Complete this partial JSON config for a '{nodeType}' node.\n" +
            "Only return the completed JSON object. Do not explain.\n\n" +
            $"Partial config:\n{partialConfig}";

        var result = await _kernel.InvokePromptAsync(prompt, cancellationToken: ct);
        return StripFences(result.GetValue<string>() ?? partialConfig);
    }

    public async Task<RemediationResult> AutoRemediateAsync(GraphDefinition graph, string failedNodeId, string error, CancellationToken ct)
    {
        var nodeList = string.Join("\n", graph.Nodes.Select(n => $"- {n.Id} ({n.Type})"));
        var failedNode = graph.Nodes.FirstOrDefault(n => n.Id == failedNodeId);
        
        var prompt =
            "You are the AgentFlow Self-Healing Agent. Analyze this workflow failure and provide a remediation plan.\n" +
            $"Failed Node: {failedNodeId} ({failedNode?.Type})\n" +
            $"Error: {error}\n\n" +
            $"Workflow Graph Nodes:\n{nodeList}\n\n" +
            "Respond ONLY with a JSON object in this format:\n" +
            "{\n" +
            "  \"canFix\": true,\n" +
            "  \"explanation\": \"Brief explanation of the fix\",\n" +
            "  \"patchedGraph\": { ... updated GraphDefinition JSON ... }\n" +
            "}";

        var result = await _kernel.InvokePromptAsync(prompt, cancellationToken: ct);
        var raw = result.GetValue<string>() ?? "{}";

        try
        {
            using var doc = JsonDocument.Parse(StripFences(raw));
            var root = doc.RootElement;
            var canFix = root.TryGetProperty("canFix", out var cf) && cf.GetBoolean();
            var explanation = root.TryGetProperty("explanation", out var exp) ? exp.GetString() ?? "" : "";
            
            GraphDefinition? patched = null;
            if (canFix && root.TryGetProperty("patchedGraph", out var pg))
            {
                patched = JsonSerializer.Deserialize<GraphDefinition>(pg.GetRawText(), AgentFlowJsonContext.Default.GraphDefinition);
            }

            return new RemediationResult(canFix, explanation, patched);
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "Failed to parse remediation result from AI");
            return new RemediationResult(false, "AI suggested a fix but it couldn't be parsed.", null);
        }
    }

    private static string StripFences(string raw)
    {
        var t = raw.Trim();
        if (t.StartsWith("```json", StringComparison.OrdinalIgnoreCase)) t = t[7..];
        else if (t.StartsWith("```")) t = t[3..];
        if (t.EndsWith("```")) t = t[..^3];
        return t.Trim();
    }
}
