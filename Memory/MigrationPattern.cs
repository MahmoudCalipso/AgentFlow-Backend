using System;
using System.Collections.Generic;

namespace AgentFlow.Backend.Memory;

public sealed record MigrationPattern
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public required string OriginalCode { get; init; }
    public required string MigratedCode { get; init; }
    public string MigrationContext { get; init; } = string.Empty;
    public float Confidence { get; init; } = 1.0f;
    public int UsageCount { get; set; }
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
    public IDictionary<string, string> Tags { get; init; } = new Dictionary<string, string>();
}
