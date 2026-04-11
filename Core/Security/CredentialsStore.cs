using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AgentFlow.Backend.Core.State;

namespace AgentFlow.Backend.Core.Security;

public interface ICredentialsStore {
    Task<T?> GetCredentialsAsync<T>(string id, CancellationToken ct) where T : class;
    Task SaveCredentialsAsync<T>(string id, T data, CancellationToken ct) where T : class;
}

public sealed class CredentialsStore : ICredentialsStore {
    private readonly IExecutionStateStore _stateStore;
    private readonly IEncryptionService _encryption;

    public CredentialsStore(IExecutionStateStore stateStore, IEncryptionService encryption) {
        _stateStore = stateStore;
        _encryption = encryption;
    }

    public async Task<T?> GetCredentialsAsync<T>(string id, CancellationToken ct) where T : class {
        var encrypted = await _stateStore.GetAsync<string>("system", $"creds:{id}", ct);
        if (string.IsNullOrEmpty(encrypted)) return null;

        var decrypted = _encryption.Decrypt(encrypted);
        return JsonSerializer.Deserialize<T>(decrypted);
    }

    public async Task SaveCredentialsAsync<T>(string id, T data, CancellationToken ct) where T : class {
        var json = JsonSerializer.Serialize(data);
        var encrypted = _encryption.Encrypt(json);
        await _stateStore.SetAsync("system", $"creds:{id}", encrypted, ct);
    }
}
