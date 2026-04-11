using System;
using System.Threading;
using System.Threading.Tasks;
using AgentFlow.Backend.Core.State;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace AgentFlow.Backend.Api;

[ApiController]
[Route("api/v1/replay")]
public sealed class ReplayController : ControllerBase
{
    private readonly IReplayService _replayService;
    private readonly ILogger<ReplayController> _log;

    public ReplayController(IReplayService replayService, ILogger<ReplayController> log)
    {
        _replayService = replayService;
        _log = log;
    }

    /// <summary>List all snapshots for a correlation ID (time-travel debug).</summary>
    [HttpGet("{correlationId}/snapshots")]
    public async Task<IActionResult> GetSnapshotsAsync(string correlationId, CancellationToken ct)
    {
        var snapshots = await _replayService.GetSnapshotsAsync(correlationId, ct);
        return Ok(new { correlation_id = correlationId, snapshots });
    }

    /// <summary>Replay a workflow from a specific snapshot with optional input overrides.</summary>
    [HttpPost("{correlationId}/replay")]
    public async Task<IActionResult> ReplayAsync(
        string correlationId,
        [FromBody] ReplayRequest request,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(correlationId))
            return BadRequest(new { error = "correlationId is required" });

        _log.LogInformation("[ReplayController] Initiating replay from snapshot {Snap} for {CorrId}",
            request.SnapshotId, correlationId);

        var newCorrId = await _replayService.ReplayAsync(correlationId, request.SnapshotId, request.InputOverrides, ct);
        return Accepted(new { new_correlation_id = newCorrId, source_correlation_id = correlationId });
    }
}

public sealed record ReplayRequest(
    string SnapshotId,
    System.Collections.Generic.Dictionary<string, object?>? InputOverrides);
