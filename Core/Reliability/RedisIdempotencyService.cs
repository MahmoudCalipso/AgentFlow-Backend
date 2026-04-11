using System;
using System.Threading;
using System.Threading.Tasks;
using StackExchange.Redis;

namespace AgentFlow.Backend.Core.Reliability;

public sealed class RedisIdempotencyService : IIdempotencyService
{
    private readonly IConnectionMultiplexer _redis;
    private const string KeyPrefix = "af:idem:";
    private static readonly TimeSpan _lockTtl = TimeSpan.FromHours(1);
    private static readonly TimeSpan _successTtl = TimeSpan.FromDays(7);

    public RedisIdempotencyService(IConnectionMultiplexer redis) => _redis = redis;

    public async Task<bool> TryLockAsync(string key, CancellationToken ct)
    {
        var db = _redis.GetDatabase();
        return await db.StringSetAsync($"{KeyPrefix}lock:{key}", "1", _lockTtl, When.NotExists);
    }

    public async Task ReleaseAsync(string key, CancellationToken ct)
    {
        var db = _redis.GetDatabase();
        await db.KeyDeleteAsync($"{KeyPrefix}lock:{key}");
    }

    public async Task<bool> HasSucceededAsync(string key, CancellationToken ct)
    {
        var db = _redis.GetDatabase();
        return await db.KeyExistsAsync($"{KeyPrefix}ok:{key}");
    }

    public async Task MarkSucceededAsync(string key, CancellationToken ct)
    {
        var db = _redis.GetDatabase();
        await db.StringSetAsync($"{KeyPrefix}ok:{key}", "1", _successTtl);
    }
}
