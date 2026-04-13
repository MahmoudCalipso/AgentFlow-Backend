using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;
using AgentFlow.Backend.Core.Execution;
using AgentFlow.Backend.Core.Observability;
using AgentFlow.Backend.Core.Policy;
using AgentFlow.Backend.Core.Nodes;
using AgentFlow.Backend.Core.Reliability;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AgentFlow.Backend.Mcp;

public sealed class McpAutoRegistrar
{
    private readonly NodeDiscoveryService _discovery;
    private readonly IHttpClientFactory _httpFactory;
    private readonly IMcpMetadataCache _cache;
    private readonly Microsoft.Extensions.Configuration.IConfiguration _config;
    private readonly ILogger<McpAutoRegistrar> _log;

    public McpAutoRegistrar(
        NodeDiscoveryService discovery,
        IHttpClientFactory httpFactory,
        IMcpMetadataCache cache,
        Microsoft.Extensions.Configuration.IConfiguration config,
        ILogger<McpAutoRegistrar> log)
    {
        _discovery = discovery;
        _httpFactory = httpFactory;
        _cache = cache;
        _config = config;
        _log = log;
    }

    public async Task RegisterAllAsync(IServiceCollection services, CancellationToken ct)
    {
        var mcpSection = _config.GetSection("AgentFlow:McpServers");
        var servers = mcpSection.GetChildren();

        _log.LogInformation("Discovered {Count} MCP endpoints in configuration.", servers.Count());

        foreach (var server in servers)
        {
            var serverName = server.Key;
            var serverUrl = server.Value;

            if (string.IsNullOrWhiteSpace(serverUrl)) continue;

            _log.LogInformation("Registering MCP server {Name} at {Url}", serverName, serverUrl);
            
            try 
            {
                var http = _httpFactory.CreateClient();
                http.BaseAddress = new Uri(serverUrl);
                var client = new McpHttpClient(http, Microsoft.Extensions.Logging.Abstractions.NullLogger<McpHttpClient>.Instance);

                // Live discovery of tools from the running container
                var tools = await client.ListToolsAsync(ct);
                var toolList = tools.Select(tool => new McpToolMetadata(tool.Name, tool.Description ?? "", serverName, serverUrl)).ToList();
                
                await _cache.UpdateCacheAsync(serverName, toolList, ct);
                RegisterTools(services, toolList);
            }
            catch (Exception ex)
            {
                _log.LogWarning("Live discovery failed for {Name} ({Url}): {Msg}", serverName, serverUrl, ex.Message);
            }
        }
    }

    private void RegisterTools(IServiceCollection services, IEnumerable<McpToolMetadata> tools)
    {
        foreach (var tool in tools)
        {
            var toolName = tool.Name;
            var serverUrl = tool.ServerUrl;

            var adapter = new McpNodeAdapter(
                toolName,
                toolName,
                null!, // Lazy initialized in adapter or via DI
                null!, 
                null!,
                null!);

            _discovery.RegisterNode(adapter);
        }
    }
}
