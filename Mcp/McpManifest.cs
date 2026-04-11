using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace AgentFlow.Backend.Mcp;

public sealed record McpManifest(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("version")] string Version,
    [property: JsonPropertyName("tools")] List<McpTool> Tools);
