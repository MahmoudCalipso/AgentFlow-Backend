using System.Text.Json.Serialization;

namespace AgentFlow.Backend.Mcp;

public sealed record McpContent(
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("text")] string? Text = null);
