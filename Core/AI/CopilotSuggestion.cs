using System.Collections.Generic;

namespace AgentFlow.Backend.Core.AI;

public sealed record CopilotSuggestion(string NodeType, string Reasoning, float Confidence, IDictionary<string, object>? SuggestedConfig = null);
