using System.Collections.Generic;

namespace AgentFlow.Backend.Core.Graph;

public sealed record GraphDefinition(
    string Id,
    string Name,
    IReadOnlyList<NodeDef> Nodes,
    IReadOnlyList<EdgeDef> Edges,
    IDictionary<string, object>? Settings = null);

public sealed record NodeDef(string Id, string Type, IDictionary<string, object>? Config = null, int? OutputPorts = null, int? InputPorts = null);

public sealed record EdgeDef(string SourceNodeId, int SourcePort, string TargetNodeId, int TargetPort);

public sealed record ValidationResult(bool IsValid, IReadOnlyList<string> Errors, IReadOnlyList<string> Warnings);
