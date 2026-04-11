using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.SemanticKernel.Embeddings;
using Microsoft.Extensions.Logging;

namespace AgentFlow.Backend.Memory;

public interface IQdrantClient
{
    Task StorePatternAsync(MigrationPattern pattern, CancellationToken ct);
    Task<IReadOnlyList<MigrationPattern>> SearchPatternsAsync(PatternQuery query, CancellationToken ct);
    Task DeletePatternAsync(Guid id, CancellationToken ct);
}
