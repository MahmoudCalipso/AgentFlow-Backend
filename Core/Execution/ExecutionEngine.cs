using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AgentFlow.Backend.Core.Nodes;
using AgentFlow.Backend.Core.State;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AgentFlow.Backend.Core.Execution;

public sealed class ExecutionEngine {
    private readonly IServiceProvider _sp;
    private readonly IExecutionStateStore _stateStore;
    private readonly ExecutionSnapshotter _snapshotter;
    private readonly ILogger<ExecutionEngine> _log;

    public ExecutionEngine(
        IServiceProvider sp,
        IExecutionStateStore stateStore,
        ExecutionSnapshotter snapshotter,
        ILogger<ExecutionEngine> log) {
        _sp = sp;
        _stateStore = stateStore;
        _snapshotter = snapshotter;
        _log = log;
    }

    public async Task ExecuteAsync(string correlationId, GraphRuntime graph, IReadOnlyList<ExecutionItem> initialItems, CancellationToken ct) {
        var stack = new ConcurrentQueue<NodeExecutionTask>();
        var waiting = new ConcurrentDictionary<string, NodeWaitingState>();
        var results = new ConcurrentDictionary<string, IReadOnlyList<IReadOnlyList<ExecutionItem>>>();

        // Start with entry nodes
        foreach (var startNodeId in graph.EntryNodes) {
            stack.Enqueue(new NodeExecutionTask(startNodeId, initialItems));
        }

        while (!stack.IsEmpty || waiting.Values.Any(v => v.IsReady)) {
            if (ct.IsCancellationRequested) break;

            if (stack.TryDequeue(out var task)) {
                await ExecuteNodeTaskAsync(correlationId, graph, task, stack, waiting, results, ct);
            } else {
                // Check if any waiting node became ready
                var readyNode = waiting.FirstOrDefault(x => x.Value.IsReady);
                if (readyNode.Key != null) {
                    if (waiting.TryRemove(readyNode.Key, out var state)) {
                        stack.Enqueue(new NodeExecutionTask(readyNode.Key, state.GetMergedItems()));
                    }
                } else {
                    await Task.Delay(10, ct); // Avoid tight loop if waiting for external triggers
                }
            }
        }
    }

    private async Task ExecuteNodeTaskAsync(
        string correlationId, 
        GraphRuntime graph, 
        NodeExecutionTask task, 
        ConcurrentQueue<NodeExecutionTask> stack,
        ConcurrentDictionary<string, NodeWaitingState> waiting,
        ConcurrentDictionary<string, IReadOnlyList<IReadOnlyList<ExecutionItem>>> results,
        CancellationToken ct) {
        
        var node = graph.Nodes[task.NodeId];
        var handler = _sp.GetRequiredKeyedService<INodeHandler>(node.Type);
        
        var creds = _sp.GetRequiredService<ICredentialsStore>();
        var secrets = _sp.GetRequiredService<ISecretManager>();
        var binary = _sp.GetRequiredService<IBinaryDataStore>();
        var ctx = new NodeContext(correlationId, graph.Id, task.InputItems, creds, secrets, binary, ct);
        var result = await handler.HandleAsync(ctx, ct);

        if (result.Success && result.Output != null) {
            results[task.NodeId] = result.Output;
            
            // Snapshot for debugging
            foreach(var outputList in result.Output) {
                foreach(var item in outputList) {
                    await _snapshotter.SnapshotAsync(correlationId, new RealTime.ExecutionDelta(task.NodeId, "node_success", item.Data, 0, DateTimeOffset.UtcNow));
                }
            }

            // Find following nodes
            if (graph.Connections.TryGetValue(task.NodeId, out var connections)) {
                foreach (var conn in connections) {
                    var outputData = result.Output.Count > conn.SourceIndex ? result.Output[conn.SourceIndex] : null;
                    if (outputData == null || outputData.Count == 0) continue;

                    var targetNode = graph.Nodes[conn.TargetNodeId];
                    if (targetNode.InputCount > 1) {
                        var state = waiting.GetOrAdd(conn.TargetNodeId, _ => new NodeWaitingState(targetNode.InputCount));
                        state.AddInput(conn.TargetInputIndex, outputData);
                    } else {
                        stack.Enqueue(new NodeExecutionTask(conn.TargetNodeId, outputData));
                    }
                }
            }
        } else {
            _log.LogError("Node {NodeId} failed: {Error}", task.NodeId, result.Error);
        }
    }
}
