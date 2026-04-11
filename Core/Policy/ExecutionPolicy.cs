using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace AgentFlow.Backend.Core.Policy;


public sealed class ExecutionPolicy : IExecutionPolicy
{
    private readonly ILogger<ExecutionPolicy> _log;
    private readonly ConcurrentDictionary<string, SlidingWindowRateLimiter> _rateLimiters = new();
    private readonly ConcurrentDictionary<string, CircuitBreakerState> _breakers = new();
    private readonly int _maxCallsPerSecond;
    private readonly int _maxMemoryMb;
    private readonly int _failureThreshold;

    public ExecutionPolicy(ILogger<ExecutionPolicy> log)
    {
        _log = log;
        _maxCallsPerSecond = 100;
        _maxMemoryMb = 2048;
        _failureThreshold = 5;
    }

    public async Task CheckAsync(string nodeId, (int CpuCores, int MemoryMb) resources, CancellationToken ct)
    {
        var breaker = _breakers.GetOrAdd(nodeId, _ => new CircuitBreakerState(_failureThreshold));

        if (breaker.IsOpen)
        {
            if (!breaker.ShouldAttemptReset())
            {
                throw new CircuitBreakerOpenException($"Circuit breaker is open for node {nodeId}. Cooling down.");
            }
        }

        if (resources.MemoryMb > _maxMemoryMb)
        {
            _log.LogWarning("Node {NodeId} requests {Mb}MB which exceeds policy limit {Max}MB", nodeId, resources.MemoryMb, _maxMemoryMb);
        }

        var limiter = _rateLimiters.GetOrAdd(nodeId, _ => new SlidingWindowRateLimiter(_maxCallsPerSecond, TimeSpan.FromSeconds(1)));

        if (!await limiter.TryAcquireAsync(ct))
        {
            throw new RateLimitExceededException($"Rate limit exceeded for node {nodeId}. Max {_maxCallsPerSecond} calls/second.");
        }
    }

    public Task RecordSuccessAsync(string nodeId)
    {
        if (_breakers.TryGetValue(nodeId, out var breaker))
        {
            breaker.RecordSuccess();
        }
        return Task.CompletedTask;
    }

    public Task RecordFailureAsync(string nodeId, string error)
    {
        var breaker = _breakers.GetOrAdd(nodeId, _ => new CircuitBreakerState(_failureThreshold));
        breaker.RecordFailure();
        _log.LogWarning("Node {NodeId} recorded failure: {Error}. Failure count: {Count}", nodeId, error, breaker.FailureCount);
        return Task.CompletedTask;
    }
}

