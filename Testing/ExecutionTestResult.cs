using System.Collections.Generic;

namespace AgentFlow.Backend.Testing;

public sealed record ExecutionTestResult(
    string CorrelationId,
    bool Succeeded,
    string? Error,
    TimeSpan Duration,
    int NodeExecutions,
    IReadOnlyList<string> ExecutionLog);
