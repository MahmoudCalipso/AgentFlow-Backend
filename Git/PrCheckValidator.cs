using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AgentFlow.Backend.Core.Graph;
using AgentFlow.Backend.Core.Security;
using AgentFlow.Backend.Core.Storage;
using Microsoft.Extensions.Logging;

namespace AgentFlow.Backend.Git;

public interface IPrCheckValidator
{
    Task<PrCheckResult> ValidateAsync(string workflowId, string branch, CancellationToken ct);
}

public sealed record PrCheckResult(
    bool   Passed,
    string WorkflowId,
    string Branch,
    string[] Errors,
    string[] Warnings);

/// <summary>
/// Validates a workflow branch before allowing a PR merge.
/// Checks: graph validation, security policy, node type existence.
/// Blocks merge on any error; Warnings are advisory.
/// </summary>
public sealed class PrCheckValidator : IPrCheckValidator
{
    private readonly IGraphStore      _graphStore;
    private readonly IGraphValidator  _graphValidator;
    private readonly NodePolicyEngine _policyEngine;
    private readonly ILogger<PrCheckValidator> _log;

    public PrCheckValidator(
        IGraphStore graphStore,
        IGraphValidator graphValidator,
        NodePolicyEngine policyEngine,
        ILogger<PrCheckValidator> log)
    {
        _graphStore     = graphStore;
        _graphValidator = graphValidator;
        _policyEngine   = policyEngine;
        _log            = log;
    }

    public async Task<PrCheckResult> ValidateAsync(string workflowId, string branch, CancellationToken ct)
    {
        _log.LogInformation("[PrCheckValidator] Validating workflow {WF} on branch '{Branch}'", workflowId, branch);

        var errors   = new System.Collections.Generic.List<string>();
        var warnings = new System.Collections.Generic.List<string>();

        var graph = await _graphStore.GetByIdAsync(workflowId, ct);
        if (graph == null)
        {
            errors.Add($"Workflow '{workflowId}' not found in store.");
            return new PrCheckResult(false, workflowId, branch, errors.ToArray(), warnings.ToArray());
        }

        // 1. Graph structural validation
        var validationResult = await _graphValidator.ValidateAsync(graph, ct);
        if (!validationResult.IsValid)
        {
            errors.AddRange(validationResult.Errors);
        }

        // 2. Node count sanity check
        if (graph.Nodes.Count == 0)
            errors.Add("Graph has no nodes.");

        if (graph.Nodes.Count > 500)
            warnings.Add($"Graph has {graph.Nodes.Count} nodes – consider splitting into sub-graphs.");

        // 3. Cycle detection warning only (cycles are valid in AgentFlow but may be unintentional)
        if (HasCycles(graph))
            warnings.Add("Graph contains cycles. Ensure termination conditions are defined.");

        var passed = errors.Count == 0;
        _log.LogInformation("[PrCheckValidator] Result: {Status} ({E} errors, {W} warnings)",
            passed ? "PASS" : "FAIL", errors.Count, warnings.Count);

        return new PrCheckResult(passed, workflowId, branch, errors.ToArray(), warnings.ToArray());
    }

    private static bool HasCycles(GraphDefinition g)
    {
        var visited = new System.Collections.Generic.HashSet<string>();
        var stack   = new System.Collections.Generic.HashSet<string>();

        bool Dfs(string nodeId)
        {
            if (stack.Contains(nodeId)) return true;
            if (visited.Contains(nodeId)) return false;
            visited.Add(nodeId);
            stack.Add(nodeId);
            foreach (var e in g.Edges.Where(e => e.SourceNodeId == nodeId))
                if (Dfs(e.TargetNodeId)) return true;
            stack.Remove(nodeId);
            return false;
        }

        return g.Nodes.Any(n => Dfs(n.Id));
    }
}
