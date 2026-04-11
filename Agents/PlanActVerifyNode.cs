using System.Text.Json;
using Microsoft.SemanticKernel;
using Microsoft.Extensions.Logging;
using AgentFlow.Backend.Core.Nodes;
using AgentFlow.Backend.Core.Policy;
using AgentFlow.Backend.Core.Observability;
using AgentFlow.Backend.Memory;
using AgentFlow.Backend.Mcp;

namespace AgentFlow.Backend.Agents;

public sealed class PlanActVerifyNode : AgenticNode {
    public PlanActVerifyNode(string nodeId, Kernel kernel, IQdrantClient memory, IMcpClient toolClient, ILogger<BaseNode> log, IExecutionPolicy pol, IAuditLogger audit) 
        : base(nodeId, kernel, memory, toolClient, log, pol, audit) {
    }

    protected override async Task<ExecutionPlan> PlanAsync(AgentState state, ExecutionItem item, NodeContext ctx, CancellationToken ct) {
        Log.LogInformation("Planning execution based on context {CorrId}", ctx.CorrelationId);
        
        // Use Semantic Kernel to invoke a planner prompt
        var prompt = "Given the data, what are the steps needed to process this?";
        var result = await Kernel.InvokePromptAsync(prompt, cancellationToken: ct);
        
        var planSteps = new List<PlanStep> {
            new PlanStep("step-1", "mcp_tool_test", item.Data)
        };
        return new ExecutionPlan(result.GetValue<string>() ?? "Default Plan", planSteps);
    }

    protected override async Task<ActionResult> ActAsync(ExecutionPlan plan, ExecutionItem item, NodeContext ctx, CancellationToken ct) {
        Log.LogInformation("Acting on plan: {Desc}", plan.Description);
        
        bool success = true;
        foreach (var step in plan.Steps) {
            try {
                var args = (step.Args as IDictionary<string, object?>) ?? new Dictionary<string, object?>();
                var toolRes = await ToolClient.CallToolAsync(step.ToolName, args, ct);
                Log.LogInformation("Tool step {ToolName} result: {Res}", step.ToolName, toolRes.ToString());
            } catch (Exception ex) {
                Log.LogError(ex, "Tool step failed");
                success = false;
            }
        }
        
        return new ActionResult(success, "Action phase complete", success ? null : "Action failed");
    }

    protected override Task<VerificationResult> VerifyAsync(ActionResult result, ExecutionItem item, NodeContext ctx, CancellationToken ct) {
        Log.LogInformation("Verifying result...");
        return Task.FromResult(new VerificationResult(result.Success, result.Success ? 1.0f : 0.0f, result.Error));
    }

    protected override async Task ReflectAsync(ExecutionPlan plan, ActionResult result, VerificationResult verify, ExecutionItem item, NodeContext ctx, CancellationToken ct) {
        if (verify.IsSuccessful && verify.Confidence >= 0.85f) {
            Log.LogInformation("Execution was highly confident, storing pattern to memory.");
            var pattern = new MigrationPattern {
                OriginalCode = "Agent Memory Input",
                MigratedCode = "Agent Memory Output",
                MigrationContext = plan.Description
            };
            await Memory.StorePatternAsync(pattern, ct);
        }
    }
}
