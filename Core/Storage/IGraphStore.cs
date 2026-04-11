using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AgentFlow.Backend.Core.Graph;

namespace AgentFlow.Backend.Core.Storage;

public interface IGraphStore
{
    Task<GraphDefinition?> GetByIdAsync(string id, CancellationToken ct);
    Task<IReadOnlyList<GraphDefinition>> ListAsync(CancellationToken ct);
    Task SaveAsync(GraphDefinition graph, CancellationToken ct);
    Task DeleteAsync(string id, CancellationToken ct);
}
