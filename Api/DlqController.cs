using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AgentFlow.Backend.Core.Reliability;
using Microsoft.AspNetCore.Mvc;

namespace AgentFlow.Backend.Api;

[ApiController]
[Route("api/dlq")]
public sealed class DlqController : ControllerBase
{
    private readonly IDeadLetterQueue _dlq;

    public DlqController(IDeadLetterQueue dlq) => _dlq = dlq;

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<DlqEntry>>> List([FromQuery] string? graphId, [FromQuery] int limit = 50, CancellationToken ct = default)
        => Ok(await _dlq.ListAsync(graphId, limit, ct));

    [HttpGet("{id}")]
    public async Task<ActionResult<DlqEntry>> Get(string id, CancellationToken ct)
    {
        var entry = await _dlq.GetAsync(id, ct);
        return entry != null ? Ok(entry) : NotFound();
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(string id, CancellationToken ct)
    {
        await _dlq.AcknowledgeAsync(id, ct);
        return NoContent();
    }
}
