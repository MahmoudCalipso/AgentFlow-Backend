using System;
using System.Collections.Generic;

namespace AgentFlow.Backend.Core.Execution;

public record ExecutionItem(
    IDictionary<string, object?> Data,
    IDictionary<string, IBinaryData>? Binary = null,
    ExecutionItem? PairedItem = null,
    string? Id = null
) {
    public string Id { get; init; } = Id ?? Guid.NewGuid().ToString();
}

public interface IBinaryData {
    string Name { get; }
    string MimeType { get; }
    long Size { get; }
    string? Directory { get; }
    string? FileExtension { get; }
    
    // Reference to the actual data in the storage provider
    string StorageId { get; }
}

public record BinaryData(
    string Name,
    string MimeType,
    long Size,
    string StorageId,
    string? Directory = null,
    string? FileExtension = null
) : IBinaryData;
