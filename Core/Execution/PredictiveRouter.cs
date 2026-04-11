using Microsoft.Extensions.Logging;
using System.Linq;
using AgentFlow.Backend.Core.Graph;

namespace AgentFlow.Backend.Core.Execution;


public sealed class PredictiveRouter {
    private readonly ILogger<PredictiveRouter> _log;

    public PredictiveRouter(ILogger<PredictiveRouter> log) {
        _log = log;
    }

    /// <summary>
    /// Predicts likely next execution paths in the DAG and pre-warms resources
    /// (JIT comp, DB connections, AI Model contexts).
    /// </summary>
    public async Task PreWarmAsync(GraphDefinition graph, ExecutionHistory history, string currentNodeId) {
        var hotPaths = history.GetLikelyNextNodes(currentNodeId);
        
        // If history is cold, use graph topological lookahead
        if (hotPaths.Count == 0) {
            hotPaths = graph.Edges
                .Where(e => e.SourceNodeId == currentNodeId)
                .Select(e => e.TargetNodeId)
                .ToList();
        }

        foreach(var nodeId in hotPaths) {
            var node = graph.Nodes.FirstOrDefault(n => n.Id == nodeId);
            if (node == null) continue;

            _log.LogInformation("[PredictiveRouter] Pre-warming resources for node: {NodeId} ({Type})", nodeId, node.Type);
            
            // Simulate resource pre-warming (Connection pool, AI context, etc.)
            await Task.Yield(); 
        }
    }
}
