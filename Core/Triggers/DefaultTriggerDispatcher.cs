using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AgentFlow.Backend.Core.Execution;
using AgentFlow.Backend.Core.Storage;
using Microsoft.Extensions.Logging;
using AgentFlow.Backend.Core.Graph;

namespace AgentFlow.Backend.Core.Triggers;

public sealed class DefaultTriggerDispatcher : ITriggerDispatcher
{
    private readonly ExecutionEngine _engine;
    private readonly IGraphStore _graphStore;
    private readonly ILogger<DefaultTriggerDispatcher> _log;

    public DefaultTriggerDispatcher(ExecutionEngine engine, IGraphStore graphStore, ILogger<DefaultTriggerDispatcher> log)
    {
        _engine = engine;
        _graphStore = graphStore;
        _log = log;
    }

    public async Task DispatchAsync(string triggerType, IDictionary<string, object?> payload, CancellationToken ct)
    {
        // Find all graphs that start with this trigger node
        var graphs = await _graphStore.ListAsync(ct);
        
        foreach (var graph in graphs)
        {
            // A trigger dispatch only activates if the triggerType matches a node that is an entry node
            // An entry node in GraphDefinition is a node with no incoming edges.
            var hasIncoming = graph.Edges.Select(e => e.TargetNodeId).ToHashSet();
            var entryNodesWithTrigger = graph.Nodes
                .Where(n => !hasIncoming.Contains(n.Id) && n.Type == triggerType)
                .ToList();

            if (entryNodesWithTrigger.Any())
            {
                var correlationId = Guid.NewGuid().ToString("N");
                _log.LogInformation("Trigger {Type} matched. Activating graph {GraphId} (CorrelationId: {CorrId})", 
                    triggerType, graph.Id, correlationId);

                var initialItem = new ExecutionItem(payload);
                
                // Convert GraphDefinition to GraphRuntime for engine
                var runtime = MapToRuntime(graph);
                
                _ = Task.Run(() => _engine.ExecuteAsync(correlationId, runtime, new[] { initialItem }, ct), ct);
            }
        }
    }

    private GraphRuntime MapToRuntime(GraphDefinition graph)
    {
        var nodes = graph.Nodes.ToDictionary(n => n.Id, n => new NodeDefinition(n.Id, n.Type, 1));
        var connections = new Dictionary<string, List<ConnectionDefinition>>();
        foreach (var edge in graph.Edges)
        {
            if (!connections.TryGetValue(edge.SourceNodeId, out var list))
                connections[edge.SourceNodeId] = list = new List<ConnectionDefinition>();
            list.Add(new ConnectionDefinition(edge.SourceNodeId, edge.SourcePort, edge.TargetNodeId, edge.TargetPort));
        }
        var hasIncoming = graph.Edges.Select(e => e.TargetNodeId).ToHashSet();
        var entries = graph.Nodes.Where(n => !hasIncoming.Contains(n.Id)).Select(n => n.Id).ToList();
        return new GraphRuntime(graph.Id, nodes, connections, entries);
    }
}
