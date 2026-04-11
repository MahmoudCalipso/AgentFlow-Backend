using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AgentFlow.Backend.Core.Graph;

namespace AgentFlow.Backend.Core.AI;

public interface IAiCopilotService
{
    Task<CopilotSuggestion> SuggestNextNodeAsync(GraphDefinition graph, string lastNodeId, CancellationToken ct);
    Task<string> ExplainErrorAsync(string nodeType, string errorMessage, string? inputDataJson, CancellationToken ct);
    Task<IReadOnlyList<OptimizationHint>> AnalyzeOptimizationAsync(GraphDefinition graph, CancellationToken ct);
    Task<string> AutoCompleteConfigAsync(string nodeType, string partialConfig, CancellationToken ct);
    Task<RemediationResult> AutoRemediateAsync(GraphDefinition graph, string failedNodeId, string error, CancellationToken ct);
}

public sealed record RemediationResult(bool CanFix, string Explanation, GraphDefinition? PatchedGraph);
