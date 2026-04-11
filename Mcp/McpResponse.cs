using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace AgentFlow.Backend.Mcp;

public sealed record McpResponse(
    [property: JsonPropertyName("content")] List<McpContent> Content,
    [property: JsonPropertyName("isError")] bool IsError = false);
