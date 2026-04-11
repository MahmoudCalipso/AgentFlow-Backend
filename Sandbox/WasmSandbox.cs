using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AgentFlow.Backend.Core.Execution;
using AgentFlow.Backend.Core.Serialization;
using Microsoft.Extensions.Logging;
using Wasmtime;

namespace AgentFlow.Backend.Sandbox;

public sealed class WasmSandbox : IDisposable 
{
    private readonly Engine _engine;
    private readonly Linker _linker;
    private readonly WasmConfig _config;
    private readonly CancellationTokenSource _epochCts = new();

    public WasmSandbox(WasmConfig config) 
    {
        var engineConfig = new Config()
            .WithEpochInterruption(true);
            
        _engine = new Engine(engineConfig);
        _linker = new Linker(_engine);
        _config = config;
        _linker.DefineWasi();

        // Background thread to increment epoch for timeouts
        Task.Run(async () => {
            while (!_epochCts.Token.IsCancellationRequested) {
                await Task.Delay(10, _epochCts.Token);
                _engine.IncrementEpoch();
            }
        }, _epochCts.Token);
    }

    public async Task<IReadOnlyList<ExecutionItem>> ExecuteItemsAsync(byte[] wasmModule, IReadOnlyList<ExecutionItem> items, CancellationToken ct) 
    {
        using var module = Module.FromBytes(_engine, "af_node", wasmModule);
        var outputs = new List<ExecutionItem>();

        foreach (var item in items) 
        {
            if (ct.IsCancellationRequested) break;

            using var store = new Store(_engine);
            // Enforce memory limit
            // Use positional arguments with long? casting as required by Wasmtime 4.0+
            store.SetLimits((long?)((ulong)_config.MemoryLimitMb * 1024 * 1024), 10000, 1, 1, 1);

            store.SetEpochDeadline((ulong)_config.TimeoutMs / 10); // Epoch increments every 10ms
            
            var wasi = new WasiConfiguration()
                .WithInheritedStandardOutput()
                .WithInheritedStandardError();
            
            store.SetWasiConfiguration(wasi);

            try {
                var instance = _linker.Instantiate(store, module);
                
                var run = instance.GetFunction("run");
                var memory = instance.GetMemory("memory");

                if (run == null || memory == null) 
                    throw new InvalidOperationException("WASM module must export 'run' function and 'memory'.");

                var inputJson = JsonSerializer.SerializeToUtf8Bytes(item.Data, AgentFlowJsonContext.Default.IDictionaryStringObject);
                
                var alloc = instance.GetFunction("allocate");
                int inputPtr = 0;
                if (alloc != null)
                {
                    inputPtr = (int)alloc.Invoke(inputJson.Length)!;
                }

                var span = memory.GetSpan(inputPtr, inputJson.Length);
                inputJson.CopyTo(span);
                
                var outputPtrVal = run.Invoke(inputPtr, inputJson.Length);
                int outputPtr = (int)outputPtrVal!;

                var outputJson = memory.ReadString(outputPtr, 1024 * 1024); 
                outputJson = outputJson.Split('\0')[0]; 
                
                var data = JsonSerializer.Deserialize<Dictionary<string, object?>>(outputJson, AgentFlowJsonContext.Default.DictionaryStringObject);
                outputs.Add(new ExecutionItem(data ?? new(), PairedItem: item));
            }
            catch (Exception ex) when (ex is WasmtimeException || ex is TrapException)
            {
                outputs.Add(new ExecutionItem(new Dictionary<string, object?> { ["error"] = ex.Message }, PairedItem: item));
            }
        }

        return outputs;
    }

    public void Dispose() 
    {
        _epochCts.Cancel();
        _engine.Dispose();
        _linker.Dispose();
        _epochCts.Dispose();
    }
}
