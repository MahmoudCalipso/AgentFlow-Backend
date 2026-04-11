using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace AgentFlow.Backend.Api;

public sealed record ExecuteRequest(
    [Required] string GraphId,
    [Required] List<NodeRequest> Nodes,
    List<ConnectionRequest> Connections,
    IDictionary<string, object?>? InputData);

public sealed record NodeRequest(string Id, string Type, bool IsEntry = false, int InputCount = 1);
public sealed record ConnectionRequest(string SourceNodeId, int SourceIndex, string TargetNodeId, int TargetIndex);
