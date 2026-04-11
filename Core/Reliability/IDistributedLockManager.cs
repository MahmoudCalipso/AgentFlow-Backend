using System;
using System.Threading;
using System.Threading.Tasks;

namespace AgentFlow.Backend.Core.Reliability;

public interface IDistributedLock : IAsyncDisposable
{
    bool IsAcquired { get; }
}

public interface IDistributedLockManager
{
    Task<IDistributedLock> AcquireAsync(string resource, TimeSpan expiry, TimeSpan wait, CancellationToken ct = default);
}
