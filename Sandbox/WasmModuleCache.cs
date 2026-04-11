using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Wasmtime;

namespace AgentFlow.Backend.Sandbox;

public sealed class WasmModuleCache : IDisposable
{
    private readonly ConcurrentDictionary<string, byte[]> _modules = new();
    private readonly string _cachePath;

    public WasmModuleCache(string cachePath)
    {
        _cachePath = cachePath;
        if (!Directory.Exists(_cachePath)) Directory.CreateDirectory(_cachePath);
    }

    public async Task<byte[]> GetOrLoadAsync(string moduleId)
    {
        if (_modules.TryGetValue(moduleId, out var cached)) return cached;

        var path = Path.Combine(_cachePath, $"{moduleId}.wasm");
        if (!File.Exists(path)) throw new FileNotFoundException($"WASM module {moduleId} not found in cache.");

        var data = await File.ReadAllBytesAsync(path);
        _modules[moduleId] = data;
        return data;
    }

    public void Store(string moduleId, byte[] data)
    {
        var path = Path.Combine(_cachePath, $"{moduleId}.wasm");
        File.WriteAllBytes(path, data);
        _modules[moduleId] = data;
    }

    public void Dispose()
    {
        _modules.Clear();
    }
}
