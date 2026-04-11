using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AgentFlow.Backend.Core.Execution;
using AgentFlow.Backend.Core.Graph;
using AgentFlow.Backend.Core.Storage;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace AgentFlow.Backend.Core.State;

public interface IReplayService
{
    Task<IReadOnlyList<ExecutionSnapshot>> GetSnapshotsAsync(string correlationId, CancellationToken ct);
    Task<string> ReplayAsync(string correlationId, string snapshotId, Dictionary<string, object?>? overrides, CancellationToken ct);
}

public sealed record ExecutionSnapshot(
    string SnapshotId,
    string CorrelationId,
    string NodeId,
    DateTimeOffset Timestamp,
    string ItemsJson);

/// <summary>
/// Time-travel replay service: fetches snapshots saved during execution and replays from any point.
/// Snapshots are stored in Redis as sorted sets ordered by timestamp.
/// </summary>
public sealed class ReplayService : IReplayService
{
    private readonly IConnectionMultiplexer _redis;
    private readonly ExecutionEngine        _engine;
    private readonly IGraphStore            _graphStore;
    private readonly ILogger<ReplayService> _log;

    private const string SnapshotKeyPrefix = "agentflow:snapshots:";

    public ReplayService(
        IConnectionMultiplexer redis,
        ExecutionEngine engine,
        IGraphStore graphStore,
        ILogger<ReplayService> log)
    {
        _redis      = redis;
        _engine     = engine;
        _graphStore = graphStore;
        _log        = log;
    }

    public async Task SaveSnapshotAsync(string correlationId, string nodeId, IReadOnlyList<ExecutionItem> items, CancellationToken ct)
    {
        var db     = _redis.GetDatabase();
        var snapId = $"{correlationId}:{nodeId}:{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}";
        var snap   = new ExecutionSnapshot(snapId, correlationId, nodeId, DateTimeOffset.UtcNow, JsonSerializer.Serialize(items));
        var score  = (double)DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        await db.SortedSetAddAsync($"{SnapshotKeyPrefix}{correlationId}", JsonSerializer.Serialize(snap), score);
        _log.LogDebug("[ReplayService] Saved snapshot {SnapId}", snapId);
    }

    public async Task<IReadOnlyList<ExecutionSnapshot>> GetSnapshotsAsync(string correlationId, CancellationToken ct)
    {
        var db      = _redis.GetDatabase();
        var entries = await db.SortedSetRangeByScoreAsync($"{SnapshotKeyPrefix}{correlationId}");
        var result  = new List<ExecutionSnapshot>();
        foreach (var entry in entries)
        {
            if (entry.HasValue)
            {
                var snap = JsonSerializer.Deserialize<ExecutionSnapshot>(entry.ToString());
                if (snap != null) result.Add(snap);
            }
        }
        return result;
    }

    public async Task<string> ReplayAsync(string correlationId, string snapshotId, Dictionary<string, object?>? overrides, CancellationToken ct)
    {
        _log.LogInformation("[ReplayService] Starting replay from snapshot {Snap} for {CorrId}", snapshotId, correlationId);

        var snapshots = await GetSnapshotsAsync(correlationId, ct);
        var snapshot  = snapshots.FirstOrDefault(s => s.SnapshotId == snapshotId);
        if (snapshot == null) throw new InvalidOperationException($"Snapshot '{snapshotId}' not found.");

        // Deserialize the saved items
        var items = JsonSerializer.Deserialize<List<Dictionary<string, object?>>>(snapshot.ItemsJson)
            ?.Select(d =>
            {
                // Apply input overrides if provided
                if (overrides != null) foreach (var kv in overrides) d[kv.Key] = kv.Value;
                return new ExecutionItem(d);
            })
            .ToList() ?? new List<ExecutionItem>();

        // Find the graph and build a runtime starting from the snapshot node
        var graphId  = correlationId.Split(':')[0]; // convention: corrId starts with graphId
        var graph    = await _graphStore.GetByIdAsync(graphId, ct);
        if (graph == null) throw new InvalidOperationException($"Graph '{graphId}' not found.");

        var newCorrId = $"replay:{Guid.NewGuid():N}";
        var runtime   = BuildRuntime(graph, snapshot.NodeId);

        _ = Task.Run(() => _engine.ExecuteAsync(newCorrId, runtime, items, ct), ct);
        return newCorrId;
    }

    private static GraphRuntime BuildRuntime(Graph.GraphDefinition graph, string startNodeId)
    {
        var nodes = graph.Nodes.ToDictionary(n => n.Id, n => new NodeDefinition(n.Id, n.Type, 1));
        var connections = new Dictionary<string, System.Collections.Generic.List<ConnectionDefinition>>();
        foreach (var edge in graph.Edges)
        {
            if (!connections.TryGetValue(edge.SourceNodeId, out var list))
                connections[edge.SourceNodeId] = list = new();
            list.Add(new ConnectionDefinition(edge.SourceNodeId, edge.SourcePort, edge.TargetNodeId, edge.TargetPort));
        }
        return new GraphRuntime(graph.Id, nodes, connections, new[] { startNodeId });
    }

    // Allow LINQ First/FirstOrDefault
    private static TSource? FirstOrDefault<TSource>(IReadOnlyList<TSource> source, Func<TSource, bool> predicate)
    {
        foreach (var item in source) if (predicate(item)) return item;
        return default;
    }
}
