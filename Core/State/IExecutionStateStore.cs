using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace AgentFlow.Backend.Core.State;

public interface IExecutionStateStore
{
    Task<T?> GetAsync<T>(string corrId, string nodeId, CancellationToken ct);
    Task SetAsync<T>(string corrId, string nodeId, T state, CancellationToken ct);
    Task AppendAsync<T>(string key, T value);
    IAsyncEnumerable<T> StreamAsync<T>(string key);
}
