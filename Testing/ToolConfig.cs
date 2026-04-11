using System;

namespace AgentFlow.Backend.Testing;

internal sealed record ToolConfig(string Name, string Description, string ResponseJson, string? ErrorMessage, TimeSpan Latency, int FailAfterCalls);
