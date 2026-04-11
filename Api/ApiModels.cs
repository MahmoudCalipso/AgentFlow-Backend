namespace AgentFlow.Backend.Api;

public sealed record CompileRequest(string Description);
public sealed record DeployOptions(string Branch = "main", string? CommitMessage = null, bool CreatePr = false, string TargetEnvironment = "dev");
