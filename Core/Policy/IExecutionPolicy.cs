namespace AgentFlow.Backend.Core.Policy;

public interface IExecutionPolicy
{
    Task CheckAsync(string nodeId, (int CpuCores, int MemoryMb) resources, CancellationToken ct);
    Task RecordSuccessAsync(string nodeId);
    Task RecordFailureAsync(string nodeId, string error);
}
