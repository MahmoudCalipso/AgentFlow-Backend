using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AgentFlow.Backend.Core.Execution;
using Microsoft.Extensions.Logging;

namespace AgentFlow.Backend.Core.Reliability;

public sealed class InMemoryDlq : IDeadLetterQueue
{
    private readonly ConcurrentDictionary<string, DlqEntry> _entries = new();
    private readonly ILogger<InMemoryDlq> _log;

    public InMemoryDlq(ILogger<InMemoryDlq> log) { _log = log; }

    public Task EnqueueAsync(DlqEntry entry, CancellationToken ct)
    {
        _entries[entry.EntryId] = entry;
        _log.LogWarning("[DLQ] Enqueued: {CorrId} node={NodeId} error={Error}", entry.CorrelationId, entry.FailedNodeId, entry.ErrorMessage);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<DlqEntry>> ListAsync(string? graphId, int limit, CancellationToken ct)
    {
        var result = new List<DlqEntry>();
        foreach (var e in _entries.Values)
        {
            if (graphId == null || e.GraphId == graphId) result.Add(e);
            if (result.Count >= limit) break;
        }
        return Task.FromResult<IReadOnlyList<DlqEntry>>(result);
    }

    public Task<DlqEntry?> GetAsync(string entryId, CancellationToken ct)
    {
        _entries.TryGetValue(entryId, out var entry);
        return Task.FromResult(entry);
    }

    public async Task<bool> RetryAsync(string entryId, ExecutionEngine engine, CancellationToken ct)
    {
        if (!_entries.TryGetValue(entryId, out var entry) || !entry.Retryable) return false;
        await AcknowledgeAsync(entryId, ct);
        return true;
    }

    public Task AcknowledgeAsync(string entryId, CancellationToken ct)
    {
        _entries.TryRemove(entryId, out _);
        return Task.CompletedTask;
    }
}
