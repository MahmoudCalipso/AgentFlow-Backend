using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace AgentFlow.Backend.Core.Reliability;

public sealed class InMemoryIdempotencyService : IIdempotencyService
{
    private readonly ConcurrentDictionary<string, byte> _locks = new();
    private readonly ConcurrentDictionary<string, byte> _successes = new();

    public Task<bool> TryLockAsync(string key, CancellationToken ct) => Task.FromResult(_locks.TryAdd(key, 0));
    public Task ReleaseAsync(string key, CancellationToken ct) { _locks.TryRemove(key, out _); return Task.CompletedTask; }
    public Task<bool> HasSucceededAsync(string key, CancellationToken ct) => Task.FromResult(_successes.ContainsKey(key));
    public Task MarkSucceededAsync(string key, CancellationToken ct) { _successes.TryAdd(key, 0); return Task.CompletedTask; }
}
