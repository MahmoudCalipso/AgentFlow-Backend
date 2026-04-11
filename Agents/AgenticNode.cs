// Original Path: Agents/AgenticNode.cs
// Line in File: 7597
using Microsoft.SemanticKernel;
using Microsoft.Extensions.Logging;
using AgentFlow.Backend.Memory;
using AgentFlow.Backend.Mcp;

namespace AgentFlow.Backend.Agents;

public abstract class AgenticNode : BaseNode
{
    protected readonly Kernel Kernel;
    protected readonly IQdrantClient Memory;
    protected readonly IMcpClient ToolClient;

    protected AgenticNode(string nodeId, Kernel kernel, IQdrantClient memory, IMcpClient toolClient,
                          ILogger<BaseNode> log, IExecutionPolicy policy, IAuditLogger audit)
        : base(nodeId, log, policy, audit)
    {
        Kernel = kernel;
        Memory = memory;
        ToolClient = toolClient;
    }

    public override async ValueTask<IReadOnlyList<IReadOnlyList<ExecutionItem>>> ExecuteAsync(NodeContext ctx, CancellationToken ct)
    {
        var outputItems = new List<ExecutionItem>();
        var threshold = ctx.GetConfig<float>(NodeId, "confidenceThreshold", 0.85f);
        var maxRetries = ctx.GetConfig<int>(NodeId, "maxRetries", 3);

        foreach (var item in ctx.InputItems)
        {
            var state = new AgentState(); // New state per item
            ExecutionItem? resultItem = null;

            for (int attempt = 1; attempt <= maxRetries; attempt++)
            {
                try
                {
                    // Update state with current item data for expression resolution
                    var itemCtx = ctx; // In a more advanced impl, we'd wrap this with item scope

                    var plan = await PlanAsync(state, item, itemCtx, ct);
                    var action = await ActAsync(plan, item, itemCtx, ct);
                    var verify = await VerifyAsync(action, item, itemCtx, ct);

                    if (verify.Confidence >= threshold)
                    {
                        await ReflectAsync(plan, action, verify, item, itemCtx, ct);
                        
                        var data = action.Output switch {
                            IDictionary<string, object?> dict => dict,
                            _ => new Dictionary<string, object?> { ["output"] = action.Output }
                        };
                        
                        resultItem = new ExecutionItem(data, PairedItem: item);
                        break;
                    }

                    state = state with { LastError = verify.Error != null ? new ExecutionError(verify.Error) : null, Attempt = attempt };
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
                catch (Exception ex) when (attempt < maxRetries)
                {
                    state = state with { LastError = new ExecutionError(ex.Message), Attempt = attempt };
                }
            }

            if (resultItem != null) {
                outputItems.Add(resultItem);
            } else {
                Log.LogWarning("Item {Id} failed to reach confidence threshold for node {NodeId}", item.Id, NodeId);
            }
        }

        return new List<List<ExecutionItem>> { outputItems };
    }

    protected abstract Task<ExecutionPlan> PlanAsync(AgentState state, ExecutionItem item, NodeContext ctx, CancellationToken ct);
    protected abstract Task<ActionResult> ActAsync(ExecutionPlan plan, ExecutionItem item, NodeContext ctx, CancellationToken ct);
    protected abstract Task<VerificationResult> VerifyAsync(ActionResult result, ExecutionItem item, NodeContext ctx, CancellationToken ct);
    protected abstract Task ReflectAsync(ExecutionPlan plan, ActionResult result, VerificationResult verify, ExecutionItem item, NodeContext ctx, CancellationToken ct);
}

public sealed record AgentState(int Attempt = 0, ExecutionError? LastError = null);
public sealed record ExecutionPlan(string Description, List<PlanStep> Steps);
public sealed record PlanStep(string Id, string ToolName, object? Args);
public sealed record ActionResult(bool Success, object? Output = null, string? Error = null);
public sealed record VerificationResult(bool IsSuccessful, float Confidence, string? Error = null);
public sealed record ExecutionError(string Message);