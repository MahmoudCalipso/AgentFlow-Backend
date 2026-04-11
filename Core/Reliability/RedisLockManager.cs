using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace AgentFlow.Backend.Core.Reliability;

public sealed class RedisLockManager : IDistributedLockManager
{
    private readonly IConnectionMultiplexer _redis;
    private readonly ILogger<RedisLockManager> _log;
    private readonly RedisValue _lockValue = Guid.NewGuid().ToString();

    public RedisLockManager(IConnectionMultiplexer redis, ILogger<RedisLockManager> log)
    {
        _redis = redis;
        _log = log;
    }

    public async Task<IDistributedLock> AcquireAsync(string resource, TimeSpan expiry, TimeSpan wait, CancellationToken ct = default)
    {
        var db = _redis.GetDatabase();
        var key = $"lock:{resource}";
        var deadline = DateTime.UtcNow.Add(wait);

        while (DateTime.UtcNow < deadline)
        {
            if (await db.LockTakeAsync(key, _lockValue, expiry))
            {
                _log.LogDebug("Acquired lock for resource {Resource}", resource);
                return new RedisLock(db, key, _lockValue, _log);
            }

            await Task.Delay(100, ct);
            if (ct.IsCancellationRequested) break;
        }

        _log.LogWarning("Failed to acquire lock for resource {Resource} after {Wait}ms", resource, wait.TotalMilliseconds);
        return new RedisLock(db, key, _lockValue, _log, isAcquired: false);
    }

    private sealed class RedisLock : IDistributedLock
    {
        private readonly IDatabase _db;
        private readonly RedisKey _key;
        private readonly RedisValue _value;
        private readonly ILogger _log;
        private int _disposed;

        public bool IsAcquired { get; }

        public RedisLock(IDatabase db, RedisKey key, RedisValue value, ILogger log, bool isAcquired = true)
        {
            _db = db;
            _key = key;
            _value = value;
            _log = log;
            IsAcquired = isAcquired;
        }

        public async ValueTask DisposeAsync()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 1) return;
            if (!IsAcquired) return;

            if (await _db.LockReleaseAsync(_key, _value))
            {
                _log.LogDebug("Released lock for {Key}", _key);
            }
            else
            {
                _log.LogWarning("Failed to release lock for {Key} (possibly expired)", _key);
            }
        }
    }
}
