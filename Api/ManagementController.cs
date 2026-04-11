using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Threading;
using System.Threading.Tasks;
using AgentFlow.Backend.Core.Execution;
using AgentFlow.Backend.Core.Tenancy;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace AgentFlow.Backend.Api;

[ApiController]
[Route("api/[controller]")]
public sealed class ManagementController : ControllerBase
{
    private readonly IExecutionStateManager _stateManager;
    private readonly ExecutionEngine _engine;
    private readonly IGraphStore _graphStore;
    private readonly AgentFlow.Backend.Core.Observability.ICostTracker _costTracker;
    private readonly ILogger<ManagementController> _log;

    public ManagementController(
        IExecutionStateManager stateManager,
        ExecutionEngine engine,
        IGraphStore graphStore,
        AgentFlow.Backend.Core.Observability.ICostTracker costTracker,
        ILogger<ManagementController> log)
    {
        _stateManager = stateManager;
        _engine = engine;
        _graphStore = graphStore;
        _costTracker = costTracker;
        _log = log;
    }

    [HttpGet("graphs")]
    public async Task<IActionResult> ListGraphsAsync(CancellationToken ct)
    {
        var graphs = await _graphStore.ListAsync(ct);
        return Ok(graphs);
    }

    [HttpPost("execute")]
    public async Task<IActionResult> ExecuteAsync([FromBody] ExecuteRequest request, CancellationToken ct)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        if (string.IsNullOrWhiteSpace(request.GraphId)) return BadRequest("GraphId is required.");

        var correlationId = Guid.NewGuid().ToString("N");
        _log.LogInformation("Executing graph {GraphId} with correlationId {CorrId}", request.GraphId, correlationId);

        var record = new ExecutionRecord(
            correlationId,
            request.GraphId,
            "running",
            DateTimeOffset.UtcNow,
            null,
            null,
            Array.Empty<string>());

        await _stateManager.SaveAsync(record, ct);

        _ = _engine.ExecuteAsync(correlationId, BuildGraphRuntime(request), BuildInitialItems(request), ct);

        return Accepted(new { correlationId, status = "running" });
    }

    [HttpGet("{correlationId}/status")]
    public async Task<IActionResult> GetStatusAsync(string correlationId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(correlationId)) return BadRequest("correlationId is required.");

        var record = await _stateManager.GetAsync(correlationId, ct);
        if (record is null) return NotFound(new { error = $"Execution {correlationId} not found." });

        return Ok(new
        {
            record.CorrelationId,
            record.GraphId,
            record.Status,
            record.StartedAt,
            record.CompletedAt,
            record.Error
        });
    }

    [HttpPost("{correlationId}/pause")]
    public async Task<IActionResult> PauseAsync(string correlationId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(correlationId)) return BadRequest();

        var record = await _stateManager.GetAsync(correlationId, ct);
        if (record is null) return NotFound();

        await _stateManager.PauseAsync(correlationId, ct);
        _log.LogInformation("Paused execution {CorrId}", correlationId);

        return Ok(new { correlationId, status = "paused" });
    }

    [HttpPost("{correlationId}/cancel")]
    public async Task<IActionResult> CancelAsync(string correlationId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(correlationId)) return BadRequest();

        var record = await _stateManager.GetAsync(correlationId, ct);
        if (record is null) return NotFound();

        await _stateManager.CancelAsync(correlationId, ct);
        _log.LogInformation("Cancelled execution {CorrId}", correlationId);

        return Ok(new { correlationId, status = "cancelled" });
    }

    [HttpGet("{correlationId}/history")]
    public async Task<IActionResult> GetHistoryAsync(string correlationId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(correlationId)) return BadRequest();

        var deltas = await _stateManager.GetDeltasAsync(correlationId, ct);
        return Ok(deltas);
    }

    [HttpGet("executions")]
    public async Task<IActionResult> ListExecutionsAsync([FromQuery] string graphId, [FromQuery] int limit = 25, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(graphId)) return BadRequest("graphId is required.");
        if (limit < 1 || limit > 200) return BadRequest("limit must be 1–200.");

        var records = await _stateManager.ListAsync(graphId, limit, ct);
        return Ok(records);
    }

    [HttpGet("tenants/{tenantId}/cost")]
    public async Task<IActionResult> GetCostReportAsync(string tenantId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(tenantId)) return BadRequest();

        var report = await _costTracker.GetReportAsync(tenantId, ct);
        return Ok(report);
    }

    private static GraphRuntime BuildGraphRuntime(ExecuteRequest request)
    {
        var nodes = new Dictionary<string, NodeDefinition>();
        var connections = new Dictionary<string, List<ConnectionDefinition>>();
        var entryNodes = new List<string>();

        foreach (var node in request.Nodes)
        {
            nodes[node.Id] = new NodeDefinition(node.Id, node.Type, node.InputCount);
            if (node.IsEntry) entryNodes.Add(node.Id);
        }

        foreach (var conn in request.Connections)
        {
            if (!connections.ContainsKey(conn.SourceNodeId))
                connections[conn.SourceNodeId] = new List<ConnectionDefinition>();
            connections[conn.SourceNodeId].Add(new ConnectionDefinition(conn.SourceNodeId, conn.SourceIndex, conn.TargetNodeId, conn.TargetIndex));
        }

        return new GraphRuntime(request.GraphId, nodes, connections, entryNodes);
    }

    private static IReadOnlyList<ExecutionItem> BuildInitialItems(ExecuteRequest request)
    {
        if (request.InputData is null || request.InputData.Count == 0)
            return new[] { new ExecutionItem(new Dictionary<string, object?>()) };

        return new[] { new ExecutionItem(request.InputData) };
    }
}
