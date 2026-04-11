using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AgentFlow.Backend.Core.Storage;
using AgentFlow.Backend.Core.Serialization;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Supabase;
using Postgrest;

namespace AgentFlow.Backend.Core.Storage.Supabase;

public sealed class SupabaseExecutionLogger : IExecutionLogger
{
    private readonly ISupabaseClientFactory _clientFactory;
    private readonly ILogger<SupabaseExecutionLogger> _log;

    public SupabaseExecutionLogger(ISupabaseClientFactory clientFactory, ILogger<SupabaseExecutionLogger> log)
    {
        _clientFactory = clientFactory;
        _log = log;
    }

    public async Task LogStartAsync(string correlationId, string graphId, CancellationToken ct)
    {
        var client = _clientFactory.CreateClient();
        var entity = new ExecutionLogEntity
        {
            Id = Guid.NewGuid().ToString("N"),
            CorrelationId = correlationId,
            GraphId = graphId,
            StartTime = DateTime.UtcNow,
            Timestamp = DateTime.UtcNow,
            Success = true
        };
        await client.From<ExecutionLogEntity>().Insert(entity);
    }

    public async Task LogEndAsync(string correlationId, string status, string? error, string? dataJson, CancellationToken ct)
    {
        var client = _clientFactory.CreateClient();
        var entity = new ExecutionLogEntity
        {
            CorrelationId = correlationId,
            EndTime = DateTime.UtcNow,
            Error = error,
            Success = error == null,
            OutputJson = dataJson,
            Timestamp = DateTime.UtcNow
        };
        await client.From<ExecutionLogEntity>().Where(x => x.CorrelationId == correlationId).Update(entity);
    }
}
