using System;
using System.Collections.Generic;

namespace AgentFlow.Backend.RealTime;

public sealed record ExecutionDelta(
    string NodeId,
    string EventType,
    IDictionary<string, object?> Data,
    int OutputPort,
    DateTimeOffset Timestamp);
