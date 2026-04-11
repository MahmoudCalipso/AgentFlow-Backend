namespace AgentFlow.Backend.Sandbox;

public record WasmConfig(int MemoryLimitMb, int TimeoutMs, bool EnableNetwork);
