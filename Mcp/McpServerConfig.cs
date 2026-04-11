namespace AgentFlow.Backend.Mcp;

public sealed record McpServerConfig(string Name, string BaseUrl, bool Required = false);
