using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AgentFlow.Backend.Core.Storage.Supabase;
using Microsoft.Extensions.Logging;
using Postgrest.Models;

namespace AgentFlow.Backend.Mcp;

public sealed class SupabaseMcpMetadataCache : IMcpMetadataCache
{
    private readonly ISupabaseClientFactory _clientFactory;
    private readonly ILogger<SupabaseMcpMetadataCache> _log;

    public SupabaseMcpMetadataCache(ISupabaseClientFactory clientFactory, ILogger<SupabaseMcpMetadataCache> log)
    {
        _clientFactory = clientFactory;
        _log = log;
    }

    public async Task<IEnumerable<McpToolMetadata>> GetCachedToolsAsync(string serverName, CancellationToken ct)
    {
        var client = _clientFactory.CreateClient();
        var response = await client.From<McpMetadataEntity>()
            .Filter("server_name", Postgrest.Constants.Operator.Equals, serverName)
            .Get();

        return response.Models.Select(m => new McpToolMetadata(m.Name, m.Description, m.ServerName, m.ServerUrl));
    }

    public async Task UpdateCacheAsync(string serverName, IEnumerable<McpToolMetadata> tools, CancellationToken ct)
    {
        var client = _clientFactory.CreateClient();
        
        // Remove old entries for this server
        await client.From<McpMetadataEntity>()
            .Filter("server_name", Postgrest.Constants.Operator.Equals, serverName)
            .Delete();

        var entities = tools.Select(t => new McpMetadataEntity
        {
            Name = t.Name,
            Description = t.Description,
            ServerName = t.ServerName,
            ServerUrl = t.ServerUrl,
            LastUpdated = DateTimeOffset.UtcNow
        }).ToList();

        if (entities.Count > 0)
        {
            await client.From<McpMetadataEntity>().Insert(entities);
            _log.LogInformation("Updated manifest cache for MCP server {ServerName} with {Count} tools", serverName, entities.Count);
        }
    }

    public async Task<IEnumerable<McpToolMetadata>> GetAllToolsAsync(CancellationToken ct)
    {
        var client = _clientFactory.CreateClient();
        var response = await client.From<McpMetadataEntity>().Get();

        return response.Models.Select(m => new McpToolMetadata(m.Name, m.Description, m.ServerName, m.ServerUrl));
    }
}
