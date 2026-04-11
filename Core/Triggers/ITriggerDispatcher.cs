using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace AgentFlow.Backend.Core.Triggers;

public interface ITriggerDispatcher
{
    Task DispatchAsync(string triggerType, IDictionary<string, object?> payload, CancellationToken ct);
}
