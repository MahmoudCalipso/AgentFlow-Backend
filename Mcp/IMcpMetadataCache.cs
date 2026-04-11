using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace AgentFlow.Backend.Mcp;

public interface IMcpMetadataCache
{
    Task<IEnumerable<McpToolMetadata>> GetCachedToolsAsync(string serverName, CancellationToken ct);
    Task UpdateCacheAsync(string serverName, IEnumerable<McpToolMetadata> tools, CancellationToken ct);
    Task<IEnumerable<McpToolMetadata>> GetAllToolsAsync(CancellationToken ct);
}
