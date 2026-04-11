using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace AgentFlow.Backend.Core.State;

public sealed class InMemoryExecutionStateStore : IExecutionStateStore
{
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, string> _store = new();
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, List<string>> _lists = new();
    private readonly JsonSerializerOptions _jsonOpts = new() { WriteIndented = false };

    public Task<T?> GetAsync<T>(string corrId, string nodeId, CancellationToken ct)
    {
        var key = BuildKey(corrId, nodeId);
        if (_store.TryGetValue(key, out var json))
        {
            return Task.FromResult(JsonSerializer.Deserialize<T>(json, _jsonOpts));
        }
        return Task.FromResult(default(T));
    }

    public Task SetAsync<T>(string corrId, string nodeId, T state, CancellationToken ct)
    {
        var key = BuildKey(corrId, nodeId);
        _store[key] = JsonSerializer.Serialize(state, _jsonOpts);
        return Task.CompletedTask;
    }

    public Task AppendAsync<T>(string key, T value)
    {
        var json = JsonSerializer.Serialize(value, _jsonOpts);
        _lists.GetOrAdd(key, _ => new List<string>()).Add(json);
        return Task.CompletedTask;
    }

    public async IAsyncEnumerable<T> StreamAsync<T>(string key)
    {
        if (_lists.TryGetValue(key, out var items))
        {
            foreach (var item in items)
            {
                var val = JsonSerializer.Deserialize<T>(item, _jsonOpts);
                if (val != null) yield return val;
            }
        }
        await Task.CompletedTask;
    }

    private static string BuildKey(string corrId, string nodeId) => $"af:state:{corrId}:{nodeId}";
}
