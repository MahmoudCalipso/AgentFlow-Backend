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
    private readonly IEnumerable<McpServerConfig> _configs;
    private readonly IHttpClientFactory _httpFactory;
    private readonly IMcpMetadataCache _cache;
    private readonly ILogger<McpAutoRegistrar> _log;

    public McpAutoRegistrar(
        IEnumerable<McpServerConfig> configs,
        IHttpClientFactory httpFactory,
        IMcpMetadataCache cache,
        ILogger<McpAutoRegistrar> log)
    {
        _configs = configs;
        _httpFactory = httpFactory;
        _cache = cache;
        _log = log;
    }

    public async Task RegisterAllAsync(IServiceCollection services, CancellationToken ct)
    {
        foreach (var config in _configs)
        {
            try
            {
                // Try cache first
                var cachedTools = (await _cache.GetCachedToolsAsync(config.Name, ct)).ToList();
                
                if (cachedTools.Count > 0)
                {
                    _log.LogInformation("Loading {Count} tools for MCP server {Name} from cache", cachedTools.Count, config.Name);
                    RegisterTools(services, cachedTools);
                }
                else
                {
                    _log.LogInformation("Cache miss for MCP server {Name}, fetching from {Url}...", config.Name, config.BaseUrl);
                    var http = _httpFactory.CreateClient();
                    http.BaseAddress = new Uri(config.BaseUrl);
                    var client = new McpHttpClient(http, Microsoft.Extensions.Logging.Abstractions.NullLogger<McpHttpClient>.Instance);

                    var tools = await client.ListToolsAsync(ct);
                    var toolList = tools.Select(t => new McpToolMetadata(t.Name, t.Description ?? "", config.Name, config.BaseUrl)).ToList();
                    
                    await _cache.UpdateCacheAsync(config.Name, toolList, ct);
                    RegisterTools(services, toolList);
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _log.LogError(ex, "Failed to register MCP server {Name} at {Url}", config.Name, config.BaseUrl);
            }
        }
    }

    private void RegisterTools(IServiceCollection services, IEnumerable<McpToolMetadata> tools)
    {
        foreach (var tool in tools)
        {
            var toolName = tool.Name;
            var serverUrl = tool.ServerUrl;

            services.AddKeyedSingleton<INodeHandler>(toolName, (sp, _) =>
            {
                var httpForNode = sp.GetRequiredService<IHttpClientFactory>().CreateClient();
                httpForNode.BaseAddress = new Uri(serverUrl);

                var mcpClient = new McpHttpClient(
                    httpForNode,
                    sp.GetRequiredService<ILogger<McpHttpClient>>());

                return new McpNodeAdapter(
                    toolName,
                    toolName,
                    mcpClient,
                    sp.GetRequiredService<ILogger<BaseNode>>(),
                    sp.GetRequiredService<IExecutionPolicy>(),
                    sp.GetRequiredService<IAuditLogger>());
            });
        }
    }
}
