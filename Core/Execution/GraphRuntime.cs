using System.Collections.Generic;

namespace AgentFlow.Backend.Core.Execution;

public sealed record GraphRuntime(
    string Id, 
    IReadOnlyDictionary<string, NodeDefinition> Nodes, 
    IReadOnlyDictionary<string, List<ConnectionDefinition>> Connections, 
    IReadOnlyList<string> EntryNodes);

public sealed record NodeDefinition(string Id, string Type, int InputCount = 1);

public sealed record ConnectionDefinition(string SourceNodeId, int SourceIndex, string TargetNodeId, int TargetInputIndex);
