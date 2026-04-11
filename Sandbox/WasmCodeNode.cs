using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AgentFlow.Backend.Core.Execution;
using AgentFlow.Backend.Core.Nodes;
using AgentFlow.Backend.Core.Policy;
using AgentFlow.Backend.Core.Observability;
using Microsoft.Extensions.Logging;

namespace AgentFlow.Backend.Sandbox;

public sealed class WasmCodeNode : BaseNode
{
    private readonly WasmSandbox _sandbox;
    private readonly WasmModuleCache _cache;

    public WasmCodeNode(
        string nodeId, 
        WasmSandbox sandbox, 
        WasmModuleCache cache,
        ILogger<BaseNode> log, 
        IExecutionPolicy policy, 
        IAuditLogger audit) 
        : base(nodeId, log, policy, audit)
    {
        _sandbox = sandbox;
        _cache = cache;
    }

    public override async ValueTask<IReadOnlyList<IReadOnlyList<ExecutionItem>>> ExecuteAsync(NodeContext ctx, CancellationToken ct)
    {
        var moduleId = ctx.GetConfig<string>(NodeId, "moduleId", "");
        if (string.IsNullOrEmpty(moduleId))
        {
            // If no moduleId, maybe actual WASM bytes are in config (from an editor)
            var base64 = ctx.GetConfig<string>(NodeId, "wasmBase64", "");
            if (string.IsNullOrEmpty(base64)) throw new InvalidOperationException("No WASM module provided.");
            
            var bytes = Convert.FromBase64String(base64);
            var results = await _sandbox.ExecuteItemsAsync(bytes, ctx.InputItems, ct);
            return new List<List<ExecutionItem>> { results.ToList() };
        }
        else
        {
            var bytes = await _cache.GetOrLoadAsync(moduleId);
            var results = await _sandbox.ExecuteItemsAsync(bytes, ctx.InputItems, ct);
            return new List<List<ExecutionItem>> { results.ToList() };
        }
    }
}
