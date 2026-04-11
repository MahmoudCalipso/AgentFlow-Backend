using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace AgentFlow.Backend.Core.State;

public sealed class RedisStateStore : IExecutionStateStore
{
    private readonly IConnectionMultiplexer _redis;
    private readonly ILogger<RedisStateStore> _log;
    private readonly JsonSerializerOptions _jsonOpts = new() { WriteIndented = false };
    private static readonly TimeSpan _ttl = TimeSpan.FromHours(24);

    public RedisStateStore(IConnectionMultiplexer redis, ILogger<RedisStateStore> log)
    {
        _redis = redis;
        _log = log;
    }

    public async Task<T?> GetAsync<T>(string corrId, string nodeId, CancellationToken ct)
    {
        var db = _redis.GetDatabase();
        var key = BuildKey(corrId, nodeId);
        RedisValue val;
        try
        {
            val = await db.StringGetAsync(key);
        }
        catch (RedisException ex)
        {
            _log.LogError(ex, "Redis GET failed for key {Key}", key);
            return default;
        }

        if (!val.HasValue) return default;
        return JsonSerializer.Deserialize<T>(val.ToString(), _jsonOpts);
    }

    public async Task SetAsync<T>(string corrId, string nodeId, T state, CancellationToken ct)
    {
        var db = _redis.GetDatabase();
        var key = BuildKey(corrId, nodeId);
        var json = JsonSerializer.Serialize(state, _jsonOpts);
        try
        {
            await db.StringSetAsync(key, json, _ttl);
        }
        catch (RedisException ex)
        {
            _log.LogError(ex, "Redis SET failed for key {Key}", key);
            throw;
        }
    }

    public async Task AppendAsync<T>(string key, T value)
    {
        var db = _redis.GetDatabase();
        var json = JsonSerializer.Serialize(value, _jsonOpts);
        try
        {
            await db.ListRightPushAsync(key, json);
            await db.KeyExpireAsync(key, _ttl);
        }
        catch (RedisException ex)
        {
            _log.LogError(ex, "Redis RPUSH failed for key {Key}", key);
            throw;
        }
    }

    public async IAsyncEnumerable<T> StreamAsync<T>(string key)
    {
        var db = _redis.GetDatabase();
        var items = await db.ListRangeAsync(key, 0, -1);
        foreach (var item in items)
        {
            if (item.HasValue)
            {
                var val = JsonSerializer.Deserialize<T>(item.ToString(), _jsonOpts);
                if (val != null) yield return val;
            }
        }
    }

    private static string BuildKey(string corrId, string nodeId) => $"af:state:{corrId}:{nodeId}";
}
