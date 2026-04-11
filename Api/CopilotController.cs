using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AgentFlow.Backend.Core.AI;
using AgentFlow.Backend.Core.Execution;
using AgentFlow.Backend.Core.Graph;
using Microsoft.AspNetCore.Mvc;

namespace AgentFlow.Backend.Api;

[ApiController]
[Route("api/copilot")]
public sealed class CopilotController : ControllerBase
{
    private readonly IAiCopilotService _copilot;

    public CopilotController(IAiCopilotService copilot) => _copilot = copilot;

    [HttpPost("suggest")]
    public async Task<ActionResult<CopilotSuggestion>> Suggest([FromBody] GraphDefinition graph, [FromQuery] string lastNodeId, CancellationToken ct)
        => Ok(await _copilot.SuggestNextNodeAsync(graph, lastNodeId, ct));

    [HttpPost("explain")]
    public async Task<ActionResult<string>> Explain([FromBody] ExplainRequest request, CancellationToken ct)
        => Ok(await _copilot.ExplainErrorAsync(request.ErrorMessage, request.NodeId, request.InputDataJson, ct));

    [HttpPost("optimize")]
    public async Task<ActionResult<IReadOnlyList<OptimizationHint>>> Optimize([FromBody] GraphDefinition graph, CancellationToken ct)
        => Ok(await _copilot.AnalyzeOptimizationAsync(graph, ct));
}

public record ExplainRequest(string ErrorMessage, string NodeId, string? InputDataJson);
