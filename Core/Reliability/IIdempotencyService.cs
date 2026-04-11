using System.Threading;
using System.Threading.Tasks;

namespace AgentFlow.Backend.Core.Reliability;

public interface IIdempotencyService
{
    Task<bool> TryLockAsync(string key, CancellationToken ct);
    Task ReleaseAsync(string key, CancellationToken ct);
    Task<bool> HasSucceededAsync(string key, CancellationToken ct);
    Task MarkSucceededAsync(string key, CancellationToken ct);
}
