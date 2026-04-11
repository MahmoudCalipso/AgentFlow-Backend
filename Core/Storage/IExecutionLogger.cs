using System;
using System.Threading;
using System.Threading.Tasks;

namespace AgentFlow.Backend.Core.Storage;

public interface IExecutionLogger
{
    Task LogStartAsync(string correlationId, string graphId, CancellationToken ct);
    Task LogEndAsync(string correlationId, string status, string? error, string? dataJson, CancellationToken ct);
}
