using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using AgentFlow.Backend.Core.Graph;
using Microsoft.Extensions.Logging;

namespace AgentFlow.Backend.Git;

public interface IJsonLdMergeStrategy
{
    Task<MergeResult> MergeAsync(GraphDefinition @base, GraphDefinition ours, GraphDefinition theirs, CancellationToken ct);
}

public sealed record MergeResult(
    bool HasConflicts,
    GraphDefinition? MergedGraph,
    IReadOnlyList<MergeConflict> Conflicts);

public sealed record MergeConflict(string NodeId, string Field, string OursValue, string TheirsValue);

/// <summary>
/// 3-way merge for workflow graph definitions.
/// Performs node-level and edge-level merges with auto-resolution where possible.
/// Conflicts are surfaced for manual review.
/// </summary>
public sealed class JsonLdMergeStrategy : IJsonLdMergeStrategy
{
    private readonly ILogger<JsonLdMergeStrategy> _log;
    public JsonLdMergeStrategy(ILogger<JsonLdMergeStrategy> log) => _log = log;

    public Task<MergeResult> MergeAsync(GraphDefinition @base, GraphDefinition ours, GraphDefinition theirs, CancellationToken ct)
    {
        var conflicts   = new List<MergeConflict>();
        var mergedNodes = new List<NodeDef>();
        var mergedEdges = new List<EdgeDef>();

        var baseNodes   = @base.Nodes.ToDictionary(n => n.Id);
        var ourNodes    = ours.Nodes.ToDictionary(n => n.Id);
        var theirNodes  = theirs.Nodes.ToDictionary(n => n.Id);
        var allNodeIds  = ourNodes.Keys.Union(theirNodes.Keys).ToHashSet();

        foreach (var id in allNodeIds)
        {
            var inBase   = baseNodes.TryGetValue(id, out var b);
            var inOurs   = ourNodes.TryGetValue(id, out var o);
            var inTheirs = theirNodes.TryGetValue(id, out var t);

            if (inBase && !inOurs && !inTheirs) continue; // deleted in both — skip
            if (!inBase && inOurs && !inTheirs) { mergedNodes.Add(o!); continue; } // added in ours
            if (!inBase && !inOurs && inTheirs) { mergedNodes.Add(t!); continue; } // added in theirs
            if (inOurs && !inTheirs)             { mergedNodes.Add(o!); continue; } // deleted in theirs, modified in ours — keep ours
            if (!inOurs && inTheirs)             { mergedNodes.Add(t!); continue; } // deleted in ours

            // Both modified — check if same result
            if (o!.Type == t!.Type)
            {
                mergedNodes.Add(o); // identical type, use ours
                continue;
            }

            // Conflict
            conflicts.Add(new MergeConflict(id, "Type", o.Type, t.Type));
            mergedNodes.Add(o); // Default: take ours on conflict
        }

        // Merge edges (simpler: union of non-deleted edges)
        var ourEdgeSet  = new HashSet<string>(ours.Edges.Select(EdgeKey));
        var theirEdgeSet= new HashSet<string>(theirs.Edges.Select(EdgeKey));
        var baseEdgeSet = new HashSet<string>(@base.Edges.Select(EdgeKey));

        var allEdges = ours.Edges.Union(theirs.Edges, EdgeEqualityComparer.Instance)
            .Where(e => !baseEdgeSet.Contains(EdgeKey(e)) || ourEdgeSet.Contains(EdgeKey(e)) || theirEdgeSet.Contains(EdgeKey(e)))
            .ToList();
        mergedEdges.AddRange(allEdges);

        var merged = @base with
        {
            Nodes = mergedNodes,
            Edges = mergedEdges,
            Settings = ours.Settings // Prefer ours for settings
        };

        _log.LogInformation("[JsonLdMerge] Merged graph {Id}: {NC} node conflicts, {EC} final edges",
            @base.Id, conflicts.Count, mergedEdges.Count);

        return Task.FromResult(new MergeResult(conflicts.Count > 0, merged, conflicts));
    }

    private static string EdgeKey(EdgeDef e) => $"{e.SourceNodeId}:{e.SourcePort}→{e.TargetNodeId}:{e.TargetPort}";

    private sealed class EdgeEqualityComparer : IEqualityComparer<EdgeDef>
    {
        public static readonly EdgeEqualityComparer Instance = new();
        public bool Equals(EdgeDef? x, EdgeDef? y) => x != null && y != null && EdgeKey(x) == EdgeKey(y);
        public int GetHashCode(EdgeDef e) => EdgeKey(e).GetHashCode(StringComparison.Ordinal);
    }
}
