using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace AgentFlow.Backend.Core.Policy;

public sealed class SlidingWindowRateLimiter
{
    private readonly int _maxCalls;
    private readonly TimeSpan _window;
    private readonly Queue<DateTimeOffset> _callTimestamps = new();
    private readonly SemaphoreSlim _lock = new(1, 1);

    public SlidingWindowRateLimiter(int maxCalls, TimeSpan window)
    {
        _maxCalls = maxCalls;
        _window = window;
    }

    public async Task<bool> TryAcquireAsync(CancellationToken ct)
    {
        await _lock.WaitAsync(ct);
        try
        {
            var now = DateTimeOffset.UtcNow;
            var cutoff = now - _window;

            while (_callTimestamps.Count > 0 && _callTimestamps.Peek() < cutoff)
            {
                _callTimestamps.Dequeue();
            }

            if (_callTimestamps.Count >= _maxCalls)
            {
                return false;
            }

            _callTimestamps.Enqueue(now);
            return true;
        }
        finally
        {
            _lock.Release();
        }
    }
}
