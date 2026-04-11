namespace AgentFlow.Backend.Core.AI;

public sealed record OptimizationHint(string HintType, string Description, string AffectedNodeId, float PotentialImpactPercent);
