using System;

namespace AgentFlow.Backend.Core.Reliability;

public sealed record DlqEntry(
    string EntryId,
    string CorrelationId,
    string GraphId,
    string FailedNodeId,
    string ErrorMessage,
    string ErrorType,
    int AttemptNumber,
    DateTimeOffset FailedAt,
    string? InputDataJson,
    bool Retryable);
