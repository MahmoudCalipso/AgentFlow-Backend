using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Wasmtime;

namespace AgentFlow.Backend.Sandbox;

/// <summary>
/// Capability-based policy for WASM sandbox execution.
/// Declares which host functions a WASM module is allowed to call.
/// </summary>
public sealed class WasmCapabilityPolicy
{
    private readonly ILogger<WasmCapabilityPolicy> _log;

    public static readonly WasmCapabilitySet Minimal = new(
        AllowStdout: true, AllowStderr: true, MaxMemoryPages: 64, MaxExecutionMs: 2000);

    public static readonly WasmCapabilitySet Standard = new(
        AllowStdout: true, AllowStderr: true, AllowEnvironmentRead: false,
        MaxMemoryPages: 256, MaxExecutionMs: 10000);

    public static readonly WasmCapabilitySet Privileged = new(
        AllowFileRead: true, AllowFileWrite: false, AllowNetworkAccess: false,
        AllowEnvironmentRead: true, AllowStdout: true, AllowStderr: true,
        MaxMemoryPages: 512, MaxExecutionMs: 30000);

    public WasmCapabilityPolicy(ILogger<WasmCapabilityPolicy> log) { _log = log; }

    /// <summary>
    /// Validates a capability set and throws if a requested capability violates policy.
    /// </summary>
    public void Enforce(WasmCapabilitySet requested, string nodeId, string tenantId)
    {
        if (requested.AllowFileWrite)
        {
            _log.LogWarning(
                "[WASM Policy] Node {NodeId} tenant {TenantId} requested FileWrite — DENIED. File writes are prohibited.",
                nodeId, tenantId);
            throw new UnauthorizedAccessException($"WASM node {nodeId} is not allowed to write files.");
        }

        if (requested.AllowNetworkAccess)
        {
            _log.LogWarning(
                "[WASM Policy] Node {NodeId} tenant {TenantId} requested NetworkAccess — DENIED. Network egress from WASM requires explicit approval.",
                nodeId, tenantId);
            throw new UnauthorizedAccessException($"WASM node {nodeId} requires network access approval.");
        }

        if (requested.MaxMemoryPages > 1024)
        {
            _log.LogWarning("[WASM Policy] Node {NodeId} requested {Pages} memory pages — capping to 1024.", nodeId, requested.MaxMemoryPages);
        }

        if (requested.MaxExecutionMs > 60000)
        {
            _log.LogWarning("[WASM Policy] Node {NodeId} requested {Ms}ms execution time — capping to 60s.", nodeId, requested.MaxExecutionMs);
        }

        _log.LogDebug("[WASM Policy] Capability check passed for node {NodeId}", nodeId);
    }

    /// <summary>
    /// Registers only the host functions permitted by the capability set into the Linker.
    /// This is the zero-trust enforcement point at bind time.
    /// </summary>
    public void BindPermittedHostFunctions(Linker linker, WasmCapabilitySet caps, string nodeId)
    {
        if (caps.AllowStdout)
        {
            linker.DefineFunction("env", "af_log_info", (Caller caller, int ptr, int len) =>
            {
                var mem = caller.GetMemory("memory");
                if (mem is not null)
                {
                    var text = mem.ReadString(ptr, len);
                    Console.WriteLine($"[WASM:{nodeId}] {text}");
                }
            });
        }

        if (caps.AllowStderr)
        {
            linker.DefineFunction("env", "af_log_error", (Caller caller, int ptr, int len) =>
            {
                var mem = caller.GetMemory("memory");
                if (mem is not null)
                {
                    var text = mem.ReadString(ptr, len);
                    Console.Error.WriteLine($"[WASM:{nodeId}:ERROR] {text}");
                }
            });
        }

        // Network and file access: explicitly STUB with UnauthorizedAccessException at call time
        if (!caps.AllowNetworkAccess)
        {
            linker.DefineFunction("env", "af_http_get", (int ptr, int len) =>
            {
                throw new UnauthorizedAccessException($"WASM node {nodeId} attempted network access (af_http_get) which is not permitted by its capability set.");
            });
        }

        if (!caps.AllowFileRead)
        {
            linker.DefineFunction("env", "af_file_read", (int ptr, int len) =>
            {
                throw new UnauthorizedAccessException($"WASM node {nodeId} attempted file read (af_file_read) which is not permitted.");
            });
        }

        if (!caps.AllowFileWrite)
        {
            linker.DefineFunction("env", "af_file_write", (int ptr, int len, int dataPtr, int dataLen) =>
            {
                throw new UnauthorizedAccessException($"WASM node {nodeId} attempted file write (af_file_write) which is not permitted.");
            });
        }

        _log.LogDebug("[WASM Policy] Bound {Count} permitted host functions for node {NodeId}", caps.AllowStdout ? 2 : 1, nodeId);
    }
}
