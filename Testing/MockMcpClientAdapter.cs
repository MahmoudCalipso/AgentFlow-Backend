using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AgentFlow.Backend.Mcp;

namespace AgentFlow.Backend.Testing;

internal sealed class MockMcpClientAdapter : IMcpClient
{
    private readonly MockMcpServer _server;
    private readonly IReadOnlyList<McpTool> _tools;

    public MockMcpClientAdapter(MockMcpServer server, IReadOnlyList<McpTool> tools)
    {
        _server = server;
        _tools = tools;
    }

    public Task<McpResponse> CallToolAsync(string tool, IDictionary<string, object?> args, CancellationToken ct = default)
        => _server.CallToolAsync(tool, args, ct);

    public Task<IEnumerable<McpTool>> ListToolsAsync(CancellationToken ct = default)
        => Task.FromResult((IEnumerable<McpTool>)_tools);
}
