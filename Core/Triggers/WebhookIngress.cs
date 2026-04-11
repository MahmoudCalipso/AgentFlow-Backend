using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AgentFlow.Backend.Core.Execution;
using AgentFlow.Backend.Core.Storage;
using Microsoft.Extensions.Logging;

namespace AgentFlow.Backend.Core.Triggers;

public sealed class WebhookIngress
{
    private readonly ExecutionEngine _engine;
    private readonly IGraphStore _graphStore;
    private readonly IExecutionLogger _execLogger;
    private readonly ILogger<WebhookIngress> _log;
    private readonly ConcurrentDictionary<string, string> _webhookMap = new(); // Path -> GraphId

    public WebhookIngress(
        ExecutionEngine engine,
        IGraphStore graphStore,
        IExecutionLogger execLogger,
        ILogger<WebhookIngress> log)
    {
        _engine = engine;
        _graphStore = graphStore;
        _execLogger = execLogger;
        _log = log;
    }

    public void Register(string path, string graphId)
    {
        _webhookMap[path] = graphId;
        _log.LogInformation("Registered webhook ingress: {Path} -> Graph {GraphId}", path, graphId);
    }

    public async Task<string> HandleRequestAsync(string path, IDictionary<string, object?> body, CancellationToken ct)
    {
        if (!_webhookMap.TryGetValue(path, out var graphId))
            throw new KeyNotFoundException($"No graph registered for webhook path: {path}");

        var graph = await _graphStore.GetByIdAsync(graphId, ct);
        if (graph == null) throw new InvalidOperationException($"Graph {graphId} not found.");

        var correlationId = Guid.NewGuid().ToString("N");
        await _execLogger.LogStartAsync(correlationId, graphId, ct);

        // Convert GraphDefinition to GraphRuntime for engine
        var runtime = MapToRuntime(graph);
        var initialItems = new[] { new ExecutionItem(body) };

        _log.LogInformation("Dispatching graph {GraphId} via webhook {Path} [CorrId: {CorrId}]", graphId, path, correlationId);
        
        // Execute fire-and-forget or await depending on requirement. 
        // For standard webhooks, usually we return 202 Accepted.
        _ = _engine.ExecuteAsync(correlationId, runtime, initialItems, ct);

        return correlationId;
    }

    private GraphRuntime MapToRuntime(Graph.GraphDefinition graph)
    {
        var nodes = graph.Nodes.ToDictionary(n => n.Id, n => new NodeDefinition(n.Id, n.Type, 1));
        var connections = new Dictionary<string, List<ConnectionDefinition>>();
        foreach (var edge in graph.Edges)
        {
            if (!connections.TryGetValue(edge.SourceNodeId, out var list))
                connections[edge.SourceNodeId] = list = new List<ConnectionDefinition>();
            list.Add(new ConnectionDefinition(edge.SourceNodeId, 0, edge.TargetNodeId, 0));
        }
        var entries = graph.Nodes.Where(n => n.Type == "webhook-trigger").Select(n => n.Id).ToList();
        return new GraphRuntime(graph.Id, nodes, connections, entries);
    }
}
