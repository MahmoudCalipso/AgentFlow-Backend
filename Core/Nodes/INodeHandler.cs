using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AgentFlow.Backend.Core.Execution;

namespace AgentFlow.Backend.Core.Nodes;

public interface INodeHandler { 
    string NodeId { get; } 
    ValueTask<NodeResult> HandleAsync(NodeContext ctx, CancellationToken ct); 
}

public sealed record NodeResult(
    bool Success, 
    IReadOnlyList<IReadOnlyList<ExecutionItem>>? Output = null, 
    string? Error = null, 
    IDictionary<string, object>? Metadata = null
) {
    public static NodeResult Ok(IReadOnlyList<IReadOnlyList<ExecutionItem>> output, IDictionary<string, object>? meta = null) 
        => new(true, output, null, meta ?? new Dictionary<string, object>());
        
    public static NodeResult Failure(string error, IDictionary<string, object>? meta = null) 
        => new(false, null, error, meta ?? new Dictionary<string, object>());
}

public sealed record WorkflowSignal(
    string CorrelationId, 
    string GraphId, 
    string NodeId, 
    IReadOnlyList<ExecutionItem> InputItems, 
    int RetryCount = 0, 
    string? ErrorContext = null
);
