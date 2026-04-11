using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AgentFlow.Backend.Core.Serialization;
using AgentFlow.Backend.Core.Execution.RealTime;
using StackExchange.Redis;

namespace AgentFlow.Backend.Core.State;

public sealed class ExecutionSnapshotter
{
    private readonly IDatabase? _redis;
    private readonly List<ExecutionDelta> _inMemoryHistory = new();

    public ExecutionSnapshotter(IConnectionMultiplexer? redis = null)
    {
        _redis = redis?.GetDatabase();
    }

    public async Task SnapshotAsync(string correlationId, ExecutionDelta delta)
    {
        if (_redis != null)
        {
            var key = $"af:exec:{correlationId}:deltas";
            var json = JsonSerializer.Serialize(delta, AgentFlowJsonContext.Default.ExecutionDelta);
            await _redis.ListRightPushAsync(key, $"{delta.NodeId}|{json}");
            await _redis.KeyExpireAsync(key, TimeSpan.FromHours(24));
        }
        else
        {
            lock (_inMemoryHistory) _inMemoryHistory.Add(delta);
        }
    }

    public async Task<List<ExecutionDelta>> GetHistoryAsync(string correlationId, CancellationToken ct)
    {
        if (_redis != null)
        {
            var key = $"af:exec:{correlationId}:deltas";
            var values = await _redis.ListRangeAsync(key);
            
            var results = new List<ExecutionDelta>();
            foreach (var v in values)
            {
                var s = v.ToString();
                var idx = s.IndexOf('|');
                if (idx == -1) continue;

                var json = s.Substring(idx + 1);
                var delta = JsonSerializer.Deserialize<ExecutionDelta>(json, AgentFlowJsonContext.Default.ExecutionDelta);
                if (delta != null) results.Add(delta);
            }
            return results;
        }
        
        lock (_inMemoryHistory) return new List<ExecutionDelta>(_inMemoryHistory);
    }
}
