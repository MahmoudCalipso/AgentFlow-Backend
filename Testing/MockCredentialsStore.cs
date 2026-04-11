using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AgentFlow.Backend.Core.Security;

namespace AgentFlow.Backend.Testing;

internal sealed class MockCredentialsStore : ICredentialsStore
{
    private readonly Dictionary<string, object> _creds = new();
    public Task<T?> GetCredentialsAsync<T>(string id, CancellationToken ct = default) where T : class 
        => Task.FromResult(_creds.GetValueOrDefault(id) as T);
    public Task SaveCredentialsAsync<T>(string id, T credentials, CancellationToken ct = default) where T : class 
        { _creds[id] = credentials; return Task.CompletedTask; }
}
