using System;
using System.Threading;
using System.Threading.Tasks;
using AgentFlow.Backend.Core.Graph;
using AgentFlow.Backend.Core.Storage;
using Microsoft.Extensions.Logging;

namespace AgentFlow.Backend.Git;

public interface IRollbackManager
{
    Task<RollbackResult> RollbackAsync(string workflowId, string tag, CancellationToken ct);
    Task TagCurrentVersionAsync(string workflowId, string tag, CancellationToken ct);
}

public sealed record RollbackResult(bool Success, string WorkflowId, string Tag, string? Error = null);

/// <summary>
/// Tag-based rollback manager: saves named snapshots of graph definitions
/// and restores them on demand. Backed by the same graph store.
/// </summary>
public sealed class RollbackManager : IRollbackManager
{
    private readonly IGraphStore _graphStore;
    private readonly ILogger<RollbackManager> _log;

    public RollbackManager(IGraphStore graphStore, ILogger<RollbackManager> log)
    {
        _graphStore = graphStore;
        _log        = log;
    }

    /// <summary>Tags the current version of a workflow with the given tag string.</summary>
    public async Task TagCurrentVersionAsync(string workflowId, string tag, CancellationToken ct)
    {
        var graph = await _graphStore.GetByIdAsync(workflowId, ct);
        if (graph == null) throw new InvalidOperationException($"Workflow {workflowId} not found.");

        // Store tagged snapshot with composite ID
        var taggedId = TagId(workflowId, tag);
        var snapshot = graph with { Id = taggedId };
        await _graphStore.SaveAsync(snapshot, ct);
        _log.LogInformation("[RollbackManager] Tagged workflow {WF} as '{Tag}'", workflowId, tag);
    }

    /// <summary>Rolls back a workflow to a previously tagged version.</summary>
    public async Task<RollbackResult> RollbackAsync(string workflowId, string tag, CancellationToken ct)
    {
        var taggedId = TagId(workflowId, tag);
        var snapshot = await _graphStore.GetByIdAsync(taggedId, ct);

        if (snapshot == null)
        {
            _log.LogError("[RollbackManager] Tag '{Tag}' not found for workflow {WF}", tag, workflowId);
            return new RollbackResult(false, workflowId, tag, $"Tag '{tag}' not found.");
        }

        // Restore the tagged version as the live graph
        var restored = snapshot with { Id = workflowId };
        await _graphStore.SaveAsync(restored, ct);

        _log.LogInformation("[RollbackManager] Workflow {WF} rolled back to tag '{Tag}'", workflowId, tag);
        return new RollbackResult(true, workflowId, tag);
    }

    private static string TagId(string workflowId, string tag) => $"{workflowId}__tag__{tag}";
}
