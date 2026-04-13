using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using System.Threading;
using System.Threading.Tasks;
using AgentFlow.Backend.Core.Serialization;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace AgentFlow.Backend.Core.State;

public sealed class RedisStateStore : IExecutionStateStore
{
    private readonly IConnectionMultiplexer _redis;
    private readonly ILogger<RedisStateStore> _log;
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
        
        var typeInfo = (JsonTypeInfo<T>)AgentFlowJsonContext.Default.GetTypeInfo(typeof(T))!;
        return JsonSerializer.Deserialize(val.ToString(), typeInfo);
    }

    public async Task SetAsync<T>(string corrId, string nodeId, T state, CancellationToken ct)
    {
        var db = _redis.GetDatabase();
        var key = BuildKey(corrId, nodeId);
        
        var typeInfo = (JsonTypeInfo<T>)AgentFlowJsonContext.Default.GetTypeInfo(typeof(T))!;
        var json = JsonSerializer.Serialize(state, typeInfo);

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
        
        var typeInfo = (JsonTypeInfo<T>)AgentFlowJsonContext.Default.GetTypeInfo(typeof(T))!;
        var json = JsonSerializer.Serialize(value, typeInfo);

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
        
        var typeInfo = (JsonTypeInfo<T>)AgentFlowJsonContext.Default.GetTypeInfo(typeof(T))!;

        foreach (var item in items)
        {
            if (item.HasValue)
            {
                var val = JsonSerializer.Deserialize(item.ToString(), typeInfo);
                if (val != null) yield return val;
            }
        }
    }

    private static string BuildKey(string corrId, string nodeId) => $"af:state:{corrId}:{nodeId}";
}
