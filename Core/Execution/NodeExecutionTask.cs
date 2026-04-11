using System.Collections.Generic;

namespace AgentFlow.Backend.Core.Execution;

public sealed record NodeExecutionTask(string NodeId, IReadOnlyList<ExecutionItem> InputItems);
