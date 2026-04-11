using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AgentFlow.Backend.Core.Security;

namespace AgentFlow.Backend.Testing;

internal sealed class MockSecretManager : ISecretManager
{
    public Task<string> GetSecretAsync(string key, CancellationToken ct = default) => Task.FromResult($"mock-secret-{key}");
    public Task<IReadOnlyDictionary<string, string>> GetAllSecretsAsync(CancellationToken ct = default) => Task.FromResult<IReadOnlyDictionary<string, string>>(new Dictionary<string, string>());
}
