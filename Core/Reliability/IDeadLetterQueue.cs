using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AgentFlow.Backend.Core.Execution;

namespace AgentFlow.Backend.Core.Reliability;

public interface IDeadLetterQueue
{
    Task EnqueueAsync(DlqEntry entry, CancellationToken ct);
    Task<IReadOnlyList<DlqEntry>> ListAsync(string? graphId, int limit, CancellationToken ct);
    Task<DlqEntry?> GetAsync(string entryId, CancellationToken ct);
    Task<bool> RetryAsync(string entryId, ExecutionEngine engine, CancellationToken ct);
    Task AcknowledgeAsync(string entryId, CancellationToken ct);
}
