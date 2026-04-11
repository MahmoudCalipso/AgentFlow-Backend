using System;
using System.Collections.Generic;

namespace AgentFlow.Backend.Core.Execution.RealTime;

public sealed record ExecutionDelta(
    string NodeId,
    string Event,
    IDictionary<string, object?> Data,
    double Cost,
    DateTimeOffset Timestamp);
