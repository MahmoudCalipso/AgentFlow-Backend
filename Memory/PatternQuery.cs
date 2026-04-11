using System.Collections.Generic;

namespace AgentFlow.Backend.Memory;

public sealed record PatternQuery(string Text, int Limit = 5, float MinConfidence = 0.7f, IDictionary<string, string>? Tags = null);
