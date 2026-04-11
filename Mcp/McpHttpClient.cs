using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;
using AgentFlow.Backend.Core.Serialization;
using Microsoft.Extensions.Logging;

namespace AgentFlow.Backend.Mcp;

public sealed class McpHttpClient : IMcpClient
{
    private readonly HttpClient _http;
    private readonly ILogger<McpHttpClient>? _log;

    public McpHttpClient(HttpClient http, ILogger<McpHttpClient>? log = null)
    {
        _http = http;
        _log = log;
    }

    public async Task<McpResponse> CallToolAsync(string tool, IDictionary<string, object?> args, CancellationToken ct = default)
    {
        var response = await _http.PostAsJsonAsync($"/tools/{tool}/execute", args, AgentFlowJsonContext.Default.IDictionaryStringObject, ct);
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<McpResponse>(AgentFlowJsonContext.Default.McpResponse, ct);
        return result ?? new McpResponse(new List<McpContent>());
    }

    public async Task<IEnumerable<McpTool>> ListToolsAsync(CancellationToken ct = default)
    {
        var tools = await _http.GetFromJsonAsync<IEnumerable<McpTool>>("/tools", AgentFlowJsonContext.Default.IEnumerableMcpTool, ct);
        return tools ?? Array.Empty<McpTool>();
    }

    public async Task<McpManifest> GetManifestAsync(CancellationToken ct = default)
    {
        var result = await _http.GetFromJsonAsync<McpManifest>("/manifest", AgentFlowJsonContext.Default.McpManifest, ct);
        return result ?? new McpManifest("unknown", "0.0.0", new List<McpTool>());
    }
}