using System.Collections.Generic;

namespace AgentFlow.Backend.Sandbox;

public sealed record WasmCapabilitySet(
    bool AllowFileRead = false,
    bool AllowFileWrite = false,
    bool AllowNetworkAccess = false,
    bool AllowEnvironmentRead = false,
    bool AllowStdout = true,
    bool AllowStderr = true,
    int MaxMemoryPages = 256,
    int MaxExecutionMs = 5000,
    IReadOnlyList<string>? AllowedHostFunctions = null);
