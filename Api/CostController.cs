using System;
using System.Threading;
using System.Threading.Tasks;
using AgentFlow.Backend.Core.Observability;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace AgentFlow.Backend.Api;

[ApiController]
[Route("api/v1/cost")]
public sealed class CostController : ControllerBase
{
    private readonly ExecutionCostTracker _tracker;
    private readonly ILogger<CostController> _log;

    public CostController(ExecutionCostTracker tracker, ILogger<CostController> log)
    {
        _tracker = tracker;
        _log     = log;
    }

    /// <summary>Get total cost attribution for a tenant.</summary>
    [HttpGet("tenant/{tenantId}")]
    public async Task<IActionResult> GetTenantCostAsync(string tenantId, CancellationToken ct)
    {
        var report = await _tracker.GetReportAsync(tenantId, ct);
        return Ok(report);
    }

    /// <summary>Track usage for an execution (called internally by nodes).</summary>
    [HttpPost("track")]
    public async Task<IActionResult> TrackUsageAsync(
        [FromBody] TrackUsageRequest req,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.TenantId) || string.IsNullOrWhiteSpace(req.CorrelationId))
            return BadRequest(new { error = "tenant_id and correlation_id are required" });

        var metrics = new ResourceMetrics(req.CpuCycles, req.MemoryBytes, req.ToolCalls, req.Tokens);
        await _tracker.TrackUsageAsync(req.TenantId, req.CorrelationId, metrics, ct);

        _log.LogInformation("[CostController] Tracked usage for tenant {T}, correlation {C}", req.TenantId, req.CorrelationId);
        return Ok(new { status = "tracked" });
    }
}

public sealed record TrackUsageRequest(
    string TenantId,
    string CorrelationId,
    long CpuCycles   = 0,
    long MemoryBytes = 0,
    int  ToolCalls   = 0,
    int  Tokens      = 0);
