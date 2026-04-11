using Microsoft.Extensions.Logging;
namespace AgentFlow.Backend.Core.Nodes;
public abstract class BaseNode : INodeHandler {
    public string NodeId { get; }
    protected readonly ILogger<BaseNode> Log;
    protected readonly IExecutionPolicy Policy;
    protected readonly IAuditLogger Audit;

    protected BaseNode(string nodeId, ILogger<BaseNode> log, IExecutionPolicy policy, IAuditLogger audit) { 
        NodeId = nodeId; Log = log; Policy = policy; Audit = audit; 
    }

    public abstract ValueTask<IReadOnlyList<IReadOnlyList<ExecutionItem>>> ExecuteAsync(NodeContext ctx, CancellationToken ct);

    public async ValueTask<NodeResult> HandleAsync(NodeContext ctx, CancellationToken ct) {
        var maxRetries = ctx.GetConfig<int>(NodeId, "maxRetries", 3);
        
        for (int attempt = 1; attempt <= maxRetries; attempt++) {
            await Policy.CheckAsync(NodeId, ctx.ResourceEstimate, ct);
            await Audit.LogStartAsync(NodeId, ctx.CorrelationId);
            try {
                var output = await ExecuteAsync(ctx, ct);
                var result = NodeResult.Ok(output);
                await Audit.LogSuccessAsync(NodeId, ctx.CorrelationId, result); 
                return result;
            } catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
            catch (Exception ex) {
                Log.LogError(ex, "{NodeId} threw exception (attempt {A}/{M})", NodeId, attempt, maxRetries);
                if (attempt >= maxRetries) return NodeResult.Failure(ex.Message);
                await Audit.LogFailureAsync(NodeId, ctx.CorrelationId, ex.Message);
            }
        }
        return NodeResult.Failure($"Exceeded max retries for {NodeId}");
    }
}
