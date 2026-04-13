using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using AgentFlow.Backend.Core.Nodes;

namespace AgentFlow.Backend.Core.Nodes;

public sealed class NodeDiscoveryService
{
    private readonly ConcurrentDictionary<string, INodeHandler> _nodes = new();

    public event Action? OnNodesUpdated;

    public void RegisterNode(INodeHandler handler)
    {
        if (_nodes.TryAdd(handler.Id, handler))
        {
            OnNodesUpdated?.Invoke();
        }
    }

    public IEnumerable<INodeHandler> GetAllNodes() => _nodes.Values;

    public INodeHandler? GetHandler(string id) => _nodes.GetValueOrDefault(id);
}
