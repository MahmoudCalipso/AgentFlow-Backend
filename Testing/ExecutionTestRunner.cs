using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AgentFlow.Backend.Core.Execution;
using AgentFlow.Backend.Core.Nodes;
using AgentFlow.Backend.Core.Observability;
using AgentFlow.Backend.Core.Policy;
using AgentFlow.Backend.Core.Security;
using AgentFlow.Backend.Core.State;
using AgentFlow.Backend.Core.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentFlow.Backend.Testing;

/// <summary>
/// Isolated execution engine for testing workflows end-to-end.
/// All external dependencies are mocked by default.
/// </summary>
public sealed class ExecutionTestRunner : IServiceProvider, IKeyedServiceProvider, IDisposable
{
    private readonly ExecutionEngine _engine;
    private readonly InMemoryExecutionStateStore _stateStore;
    private readonly ExecutionSnapshotter _snapshotter;
    private readonly Dictionary<string, INodeHandler> _nodeHandlers = new();
    private readonly Dictionary<Type, object> _services = new();
    private readonly List<string> _executionLog = new();

    public IReadOnlyList<string> ExecutionLog => _executionLog;

    public ExecutionTestRunner()
    {
        _stateStore = new InMemoryExecutionStateStore();
        _snapshotter = new ExecutionSnapshotter();

        // Core mocks
        _services[typeof(ICredentialsStore)] = new MockCredentialsStore();
        _services[typeof(ISecretManager)] = new MockSecretManager();
        _services[typeof(IBinaryDataStore)] = new LocalBinaryDataStore("/tmp/af-test", NullLogger<LocalBinaryDataStore>.Instance);
        _services[typeof(IExecutionStateStore)] = _stateStore;
        _services[typeof(ExecutionSnapshotter)] = _snapshotter;

        _engine = new ExecutionEngine(this, _stateStore, _snapshotter, NullLogger<ExecutionEngine>.Instance);
    }

    public object? GetService(Type serviceType) => _services.GetValueOrDefault(serviceType);

    public object? GetKeyedService(Type serviceType, object? serviceKey)
    {
        if (serviceType == typeof(INodeHandler) && serviceKey is string typeName)
        {
            return _nodeHandlers.GetValueOrDefault(typeName);
        }
        return GetService(serviceType);
    }

    public object GetRequiredKeyedService(Type serviceType, object? serviceKey)
    {
        return GetKeyedService(serviceType, serviceKey) 
            ?? throw new InvalidOperationException($"No keyed service found for {serviceType.Name} with key {serviceKey}");
    }

    /// <summary>Registers a mock node handler by type name.</summary>
    public ExecutionTestRunner WithNode(string nodeType, Func<NodeContext, CancellationToken, Task<IReadOnlyList<IReadOnlyList<ExecutionItem>>>> handler)
    {
        _nodeHandlers[nodeType] = new LambdaNodeHandler(nodeType, async (ctx, ct) => 
        {
            lock (_executionLog) _executionLog.Add($"Executing node: {ctx.GraphId}/{nodeType}");
            var output = await handler(ctx, ct);
            return NodeResult.Ok(output);
        });
        return this;
    }

    /// <summary>Registers a simple pass-through node that returns its input unchanged.</summary>
    public ExecutionTestRunner WithPassthroughNode(string nodeType)
    {
        _nodeHandlers[nodeType] = new LambdaNodeHandler(nodeType, (ctx, ct) =>
        {
            var outputs = new List<List<ExecutionItem>> { ctx.InputItems.ToList() };
            return ValueTask.FromResult(NodeResult.Ok(outputs));
        });
        return this;
    }

    /// <summary>Registers a node that always throws an exception.</summary>
    public ExecutionTestRunner WithFailingNode(string nodeType, string errorMessage)
    {
        _nodeHandlers[nodeType] = new LambdaNodeHandler(nodeType, (ctx, ct) =>
            throw new InvalidOperationException($"[TestRunner] Injected failure: {errorMessage}"));
        return this;
    }

    /// <summary>
    /// Executes the given graph topology with the given initial items.
    /// Returns the final output items from all terminal nodes.
    /// </summary>
    public async Task<ExecutionTestResult> RunAsync(
        GraphRuntime graph,
        IReadOnlyList<ExecutionItem> inputItems,
        CancellationToken ct = default)
    {
        _executionLog.Clear();
        var corrId = $"test-{Guid.NewGuid():N}";

        var startedAt = DateTimeOffset.UtcNow;
        Exception? error = null;

        try
        {
            await _engine.ExecuteAsync(corrId, graph, inputItems, ct);
        }
        catch (Exception ex)
        {
            error = ex;
        }

        // In the new engine, state is in _stateStore
        // We don't have GetDeltasAsync anymore, but we can verify results via _stateStore if needed
        // For now, we return based on Success/Error

        return new ExecutionTestResult(
            CorrelationId: corrId,
            Succeeded: error is null,
            Error: error?.Message,
            Duration: DateTimeOffset.UtcNow - startedAt,
            NodeExecutions: _executionLog.Count, 
            ExecutionLog: _executionLog.ToList());
    }

    /// <summary>
    /// Assert that the result succeeded. Throws with the error message if not.
    /// </summary>
    public static void AssertSucceeded(ExecutionTestResult result)
    {
        if (!result.Succeeded)
            throw new InvalidOperationException($"Execution failed: {result.Error}");
    }

    /// <summary>
    /// Assert that the result failed with a specific error message fragment.
    /// </summary>
    public static void AssertFailed(ExecutionTestResult result, string errorFragment)
    {
        if (result.Succeeded)
            throw new InvalidOperationException("Expected execution to fail, but it succeeded.");
        if (result.Error?.Contains(errorFragment, StringComparison.OrdinalIgnoreCase) != true)
            throw new InvalidOperationException($"Expected error containing '{errorFragment}', got: '{result.Error}'");
    }

    public void Dispose() { }
}
