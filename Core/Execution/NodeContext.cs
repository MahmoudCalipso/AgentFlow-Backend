using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using AgentFlow.Backend.Core.Security;
using AgentFlow.Backend.Core.Storage;

namespace AgentFlow.Backend.Core.Execution;
public sealed class NodeContext {
    private readonly ConcurrentDictionary<string, object?> _state = new();
    public string CorrelationId { get; } 
    public string GraphId { get; } 
    public IReadOnlyList<ExecutionItem> InputItems { get; } 
    public CancellationToken CancellationToken { get; }
    
    public ICredentialsStore Credentials { get; }
    public ISecretManager Secrets { get; }
    public IBinaryDataStore Binary { get; }

    public NodeContext(
        string correlationId, 
        string graphId, 
        IReadOnlyList<ExecutionItem> items, 
        ICredentialsStore creds,
        ISecretManager secrets,
        IBinaryDataStore binary,
        CancellationToken ct) { 
        CorrelationId = correlationId; 
        GraphId = graphId; 
        InputItems = items; 
        Credentials = creds;
        Secrets = secrets;
        Binary = binary;
        CancellationToken = ct; 
    }
    public T? GetState<T>(string key) where T : class => _state.TryGetValue(key, out var v) ? v as T : null;
    public void SetState<T>(string key, T value) where T : class => _state[key] = value;
    public T GetConfig<T>(string nodeId, string key, T defaultValue) => _state.TryGetValue($"{nodeId}:config:{key}", out var v) ? (T)v! : defaultValue;
    public void SetConfig(string nodeId, string key, object value) => _state[$"{nodeId}:config:{key}"] = value;
    public (int CpuCores, int MemoryMb) ResourceEstimate => (Environment.ProcessorCount, 512);
}
