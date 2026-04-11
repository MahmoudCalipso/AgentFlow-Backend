using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace AgentFlow.Backend.Core.Execution;

public sealed class ExecutionHistory
{
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, int>> _transitions = new();

    public void RecordTransition(string fromNodeId, string toNodeId)
    {
        var nodeTransitions = _transitions.GetOrAdd(fromNodeId, _ => new ConcurrentDictionary<string, int>());
        nodeTransitions.AddOrUpdate(toNodeId, 1, (_, count) => count + 1);
    }

    public IReadOnlyList<string> GetLikelyNextNodes(string fromNodeId, int limit = 3)
    {
        if (!_transitions.TryGetValue(fromNodeId, out var nodeTransitions))
        {
            return Array.Empty<string>();
        }

        return nodeTransitions
            .OrderByDescending(x => x.Value)
            .Take(limit)
            .Select(x => x.Key)
            .ToList();
    }
}
