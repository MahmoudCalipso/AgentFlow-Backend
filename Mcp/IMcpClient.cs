using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace AgentFlow.Backend.Mcp;

public interface IMcpClient
{
    Task<McpResponse> CallToolAsync(string tool, IDictionary<string, object?> args, CancellationToken ct = default);
    Task<IEnumerable<McpTool>> ListToolsAsync(CancellationToken ct = default);
}
