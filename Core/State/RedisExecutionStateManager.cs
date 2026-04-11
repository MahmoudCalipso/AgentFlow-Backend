using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AgentFlow.Backend.Core.Serialization;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace AgentFlow.Backend.Core.State;

public sealed class RedisExecutionStateManager : IExecutionStateManager
{
    private readonly IConnectionMultiplexer _redis;
    private readonly ILogger<RedisExecutionStateManager> _log;
    private readonly IDatabase _db;

    public RedisExecutionStateManager(IConnectionMultiplexer redis, ILogger<RedisExecutionStateManager> log)
    {
        _redis = redis;
        _log = log;
        _db = redis.GetDatabase();
    }

    private string RecordKey(string correlationId) => $"exec:record:{correlationId}";
    private string DeltasKey(string correlationId) => $"exec:deltas:{correlationId}";
    private string GraphIndexKey(string graphId) => $"exec:index:graph:{graphId}";

    public async Task<ExecutionRecord?> GetAsync(string correlationId, CancellationToken ct)
    {
        var data = await _db.StringGetAsync(RecordKey(correlationId));
        if (data.IsNull) return null;

        return JsonSerializer.Deserialize((string)data!, AgentFlowJsonContext.Default.ExecutionRecord);
    }

    public async Task SaveAsync(ExecutionRecord record, CancellationToken ct)
    {
        var json = JsonSerializer.Serialize(record, AgentFlowJsonContext.Default.ExecutionRecord);
        var batch = _db.CreateBatch();
        
        var recordTask = batch.StringSetAsync(RecordKey(record.CorrelationId), json, TimeSpan.FromDays(7));
        var indexTask = batch.SortedSetAddAsync(GraphIndexKey(record.GraphId), record.CorrelationId, record.StartedAt.ToUnixTimeMilliseconds());
        
        batch.Execute();
        await Task.WhenAll(recordTask, indexTask);
        
        _log.LogDebug("Saved execution record for {CorrId}", record.CorrelationId);
    }

    public async Task<IReadOnlyList<ExecutionRecord>> ListAsync(string graphId, int limit, CancellationToken ct)
    {
        var ids = await _db.SortedSetRangeByRankAsync(GraphIndexKey(graphId), 0, limit - 1, Order.Descending);
        if (ids.Length == 0) return Array.Empty<ExecutionRecord>();

        var keys = ids.Select(id => (RedisKey)RecordKey(id!)).ToArray();
        var values = await _db.StringGetAsync(keys);

        var results = new List<ExecutionRecord>();
        foreach (var val in values)
        {
            if (val.IsNull) continue;
            var record = JsonSerializer.Deserialize((string)val!, AgentFlowJsonContext.Default.ExecutionRecord);
            if (record != null) results.Add(record);
        }

        return results;
    }

    public async Task PauseAsync(string correlationId, CancellationToken ct)
    {
        var record = await GetAsync(correlationId, ct);
        if (record != null)
        {
            await SaveAsync(record with { Status = "paused" }, ct);
            _log.LogInformation("Execution {CorrId} set to paused in Redis", correlationId);
        }
    }

    public async Task CancelAsync(string correlationId, CancellationToken ct)
    {
        var record = await GetAsync(correlationId, ct);
        if (record != null)
        {
            await SaveAsync(record with { Status = "cancelled", CompletedAt = DateTimeOffset.UtcNow }, ct);
            _log.LogInformation("Execution {CorrId} set to cancelled in Redis", correlationId);
        }
    }

    public async Task<IReadOnlyList<ExecutionDeltaRecord>> GetDeltasAsync(string correlationId, CancellationToken ct)
    {
        var deltas = await _db.ListRangeAsync(DeltasKey(correlationId));
        if (deltas.Length == 0) return Array.Empty<ExecutionDeltaRecord>();

        var results = new List<ExecutionDeltaRecord>();
        foreach (var d in deltas)
        {
            var delta = JsonSerializer.Deserialize((string)d!, AgentFlowJsonContext.Default.ExecutionDeltaRecord);
            if (delta != null) results.Add(delta);
        }
        return results;
    }

    public async Task AppendDeltaAsync(string correlationId, ExecutionDeltaRecord delta)
    {
        var json = JsonSerializer.Serialize(delta, AgentFlowJsonContext.Default.ExecutionDeltaRecord);
        await _db.ListRightPushAsync(DeltasKey(correlationId), json);
        await _db.KeyExpireAsync(DeltasKey(correlationId), TimeSpan.FromDays(7));
    }
}
