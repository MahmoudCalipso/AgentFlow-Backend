using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AgentFlow.Backend.Mcp;
using AgentFlow.Backend.Core.Serialization;
using Microsoft.Extensions.Logging;

namespace AgentFlow.Backend.Testing;

public sealed class MockMcpServer : IDisposable
{
    private readonly ConcurrentDictionary<string, ToolConfig> _tools = new();
    private readonly List<ToolCall> _callLog = new();
    private readonly SemaphoreSlim _logLock = new(1, 1);
    private readonly ILogger<MockMcpServer> _log;
    private int _callCount;

    public IReadOnlyList<ToolCall> CallLog { get { lock (_callLog) return _callLog.ToList(); } }
    public int TotalCalls => _callCount;

    public MockMcpServer(ILogger<MockMcpServer> log)
    {
        _log = log;
    }

    public MockMcpServer WithTool(string name, string description, object response)
    {
        var mcpResponse = response is McpResponse r ? r : new McpResponse(new List<McpContent> { new McpContent("text", JsonSerializer.Serialize(response, AgentFlowJsonContext.Default.Options)) });
        _tools[name] = new ToolConfig(name, description, JsonSerializer.Serialize(mcpResponse, AgentFlowJsonContext.Default.McpResponse), null, TimeSpan.Zero, 0);
        return this;
    }

    public async Task<McpResponse> CallToolAsync(string toolName, IDictionary<string, object?> args, CancellationToken ct)
    {
        Interlocked.Increment(ref _callCount);

        if (!_tools.TryGetValue(toolName, out var config))
            throw new InvalidOperationException($"MockMcpServer: unknown tool '{toolName}'");

        var callIndex = _callCount;
        await _logLock.WaitAsync(ct);
        try
        {
            _callLog.Add(new ToolCall(toolName, JsonSerializer.Serialize(args, AgentFlowJsonContext.Default.IDictionaryStringObject), DateTimeOffset.UtcNow, callIndex));
        }
        finally { _logLock.Release(); }

        if (config.Latency > TimeSpan.Zero)
            await Task.Delay(config.Latency, ct);

        if (config.ErrorMessage is not null && (config.FailAfterCalls == 0 || callIndex > config.FailAfterCalls))
        {
            throw new InvalidOperationException($"[MockMcpServer] Injected failure: {config.ErrorMessage}");
        }

        return JsonSerializer.Deserialize<McpResponse>(config.ResponseJson, AgentFlowJsonContext.Default.McpResponse)!;
    }

    public IMcpClient CreateClient() => new MockMcpClientAdapter(this, _tools.Values.Select(t => new McpTool(t.Name, t.Description)).ToList());

    public void Dispose() { _logLock.Dispose(); }
}
