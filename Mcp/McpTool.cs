using System.Text.Json;
using System.Text.Json.Serialization;

namespace AgentFlow.Backend.Mcp;

public sealed record McpTool(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("description")] string Description,
    [property: JsonPropertyName("inputSchema")] JsonElement? InputSchema = null);
