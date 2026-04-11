using System;
using System.Threading;

namespace AgentFlow.Backend.Core.Policy;

public sealed class CircuitBreakerState
{
    private readonly int _threshold;
    private int _failureCount;
    private DateTimeOffset _openedAt;
    private CircuitState _state = CircuitState.Closed;
    private static readonly TimeSpan _cooldown = TimeSpan.FromSeconds(30);

    public CircuitBreakerState(int threshold) { _threshold = threshold; }

    public bool IsOpen => _state == CircuitState.Open;
    public int FailureCount => _failureCount;

    public void RecordFailure()
    {
        var count = Interlocked.Increment(ref _failureCount);
        if (count >= _threshold)
        {
            _state = CircuitState.Open;
            _openedAt = DateTimeOffset.UtcNow;
        }
    }

    public void RecordSuccess()
    {
        Interlocked.Exchange(ref _failureCount, 0);
        _state = CircuitState.Closed;
    }

    public bool ShouldAttemptReset()
    {
        if (_state == CircuitState.Open && DateTimeOffset.UtcNow - _openedAt >= _cooldown)
        {
            _state = CircuitState.HalfOpen;
            return true;
        }
        return false;
    }

    private enum CircuitState { Closed, Open, HalfOpen }
}
