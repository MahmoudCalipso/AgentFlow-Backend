using System;

namespace AgentFlow.Backend.Testing;

public sealed record ToolCall(string ToolName, string ArgsJson, DateTimeOffset CalledAt, int CallIndex);
