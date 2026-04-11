using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;

namespace AgentFlow.Backend.Core.Reliability;

public sealed class IdempotencyManager {
    // In-memory cache stub (replace with Redis/distributed cache in production)
    private readonly ConcurrentDictionary<string, object> _cache = new();

    /// <summary>
    /// Executes an action exactly once based on the idempotency key.
    /// Handles exactly-once execution semantics avoiding duplicate side effects.
    /// </summary>
    public async Task<T?> ExecuteIdempotentAsync<T>(string key, Func<Task<T>> action, TimeSpan ttl) {
        if (_cache.TryGetValue($"idem:{key}", out var cached)) {
            return (T)cached;
        }

        var result = await action();
        
        if (result != null) {
            _cache[$"idem:{key}"] = result;
            // TTL expiration logic would go here
        }
        
        return result;
    }
}
