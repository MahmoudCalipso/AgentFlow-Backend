using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AgentFlow.Backend.Core.Execution;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace AgentFlow.Backend.Core.State;

public interface IExecutionStateManager
{
    Task<ExecutionRecord?> GetAsync(string correlationId, CancellationToken ct);
    Task SaveAsync(ExecutionRecord record, CancellationToken ct);
    Task<IReadOnlyList<ExecutionRecord>> ListAsync(string graphId, int limit, CancellationToken ct);
    Task PauseAsync(string correlationId, CancellationToken ct);
    Task CancelAsync(string correlationId, CancellationToken ct);
    Task<IReadOnlyList<ExecutionDeltaRecord>> GetDeltasAsync(string correlationId, CancellationToken ct);
}

public sealed record ExecutionRecord(
    string CorrelationId,
    string GraphId,
    string Status,
    DateTimeOffset StartedAt,
    DateTimeOffset? CompletedAt,
    string? Error,
    IReadOnlyList<string> NodeIds);

public sealed record ExecutionDeltaRecord(
    string NodeId,
    string Event,
    DateTimeOffset Timestamp,
    IDictionary<string, object?> Data);

public sealed class InMemoryExecutionStateManager : IExecutionStateManager
{
    private readonly ConcurrentDictionary<string, ExecutionRecord> _records = new();
    private readonly ConcurrentDictionary<string, List<ExecutionDeltaRecord>> _deltas = new();
    private readonly ILogger<InMemoryExecutionStateManager> _log;

    public InMemoryExecutionStateManager(ILogger<InMemoryExecutionStateManager> log)
    {
        _log = log;
    }

    public Task<ExecutionRecord?> GetAsync(string correlationId, CancellationToken ct)
    {
        _records.TryGetValue(correlationId, out var record);
        return Task.FromResult(record);
    }

    public Task SaveAsync(ExecutionRecord record, CancellationToken ct)
    {
        _records[record.CorrelationId] = record;
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<ExecutionRecord>> ListAsync(string graphId, int limit, CancellationToken ct)
    {
        var result = new List<ExecutionRecord>();
        foreach (var r in _records.Values)
        {
            if (r.GraphId == graphId) result.Add(r);
            if (result.Count >= limit) break;
        }
        return Task.FromResult<IReadOnlyList<ExecutionRecord>>(result);
    }

    public Task PauseAsync(string correlationId, CancellationToken ct)
    {
        if (_records.TryGetValue(correlationId, out var record))
        {
            _records[correlationId] = record with { Status = "paused" };
            _log.LogInformation("Execution {CorrId} paused", correlationId);
        }
        return Task.CompletedTask;
    }

    public Task CancelAsync(string correlationId, CancellationToken ct)
    {
        if (_records.TryGetValue(correlationId, out var record))
        {
            _records[correlationId] = record with { Status = "cancelled", CompletedAt = DateTimeOffset.UtcNow };
            _log.LogInformation("Execution {CorrId} cancelled", correlationId);
        }
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<ExecutionDeltaRecord>> GetDeltasAsync(string correlationId, CancellationToken ct)
    {
        _deltas.TryGetValue(correlationId, out var deltas);
        return Task.FromResult<IReadOnlyList<ExecutionDeltaRecord>>(deltas ?? new List<ExecutionDeltaRecord>());
    }

    public void AppendDelta(string correlationId, ExecutionDeltaRecord delta)
    {
        _deltas.GetOrAdd(correlationId, _ => new List<ExecutionDeltaRecord>()).Add(delta);
    }
}
