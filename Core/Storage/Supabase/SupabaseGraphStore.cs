using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AgentFlow.Backend.Core.Graph;
using AgentFlow.Backend.Core.Serialization;
using Microsoft.Extensions.Logging;

namespace AgentFlow.Backend.Core.Storage.Supabase;

public sealed class SupabaseGraphStore : IGraphStore
{
    private readonly ISupabaseClientFactory _factory;
    private readonly ILogger<SupabaseGraphStore> _log;

    public SupabaseGraphStore(ISupabaseClientFactory factory, ILogger<SupabaseGraphStore> log)
    {
        _factory = factory;
        _log = log;
    }

    public async Task<GraphDefinition?> GetByIdAsync(string id, CancellationToken ct)
    {
        var client = _factory.CreateClient();
        var response = await client.From<GraphEntity>()
            .Where(x => x.Id == id)
            .Get(ct);

        var entity = response.Models.FirstOrDefault();
        if (entity == null) return null;

        return JsonSerializer.Deserialize<GraphDefinition>(entity.DefinitionJson, AgentFlowJsonContext.Default.GraphDefinition);
    }

    public async Task<IReadOnlyList<GraphDefinition>> ListAsync(CancellationToken ct)
    {
        var client = _factory.CreateClient();
        var response = await client.From<GraphEntity>().Get(ct);
        
        return response.Models.Select(m => JsonSerializer.Deserialize<GraphDefinition>(m.DefinitionJson, AgentFlowJsonContext.Default.GraphDefinition)!).ToList();
    }

    public async Task SaveAsync(GraphDefinition graph, CancellationToken ct)
    {
        var client = _factory.CreateClient();
        var entity = new GraphEntity
        {
            Id = graph.Id,
            Name = graph.Name ?? graph.Id,
            DefinitionJson = JsonSerializer.Serialize(graph, AgentFlowJsonContext.Default.GraphDefinition),
            UpdatedAt = DateTime.UtcNow
        };

        await client.From<GraphEntity>().Upsert(entity);
        _log.LogInformation("Saved graph {Id} to Supabase", graph.Id);
    }

    public async Task DeleteAsync(string id, CancellationToken ct)
    {
        var client = _factory.CreateClient();
        await client.From<GraphEntity>().Where(x => x.Id == id).Delete();
    }
}
