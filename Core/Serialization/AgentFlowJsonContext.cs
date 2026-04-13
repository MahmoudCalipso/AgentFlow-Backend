using System.Text.Json.Serialization;
using AgentFlow.Backend.Mcp;
using AgentFlow.Backend.Core.Execution;
using AgentFlow.Backend.Core.Execution.RealTime;
using AgentFlow.Backend.Core.AI;
using AgentFlow.Backend.Core.Nodes;
using AgentFlow.Backend.Core.Graph;
using AgentFlow.Backend.Core.State;
using AgentFlow.Backend.Core.Observability;
using AgentFlow.Backend.Api;
using AgentFlow.Backend.Marketplace;
using System.Collections.Generic;
using System.Text.Json;

namespace AgentFlow.Backend.Core.Serialization;

[JsonSerializable(typeof(ExecutionRecord))]
[JsonSerializable(typeof(ExecutionDeltaRecord))]
[JsonSerializable(typeof(List<ExecutionDeltaRecord>))]

[JsonSerializable(typeof(McpToolMetadata))]
[JsonSerializable(typeof(IEnumerable<McpToolMetadata>))]
[JsonSerializable(typeof(List<McpToolMetadata>))]
[JsonSerializable(typeof(McpMetadataEntity))]
[JsonSerializable(typeof(RemediationResult))]
[JsonSerializable(typeof(McpManifest))]
[JsonSerializable(typeof(McpTool))]
[JsonSerializable(typeof(List<McpTool>))]
[JsonSerializable(typeof(IEnumerable<McpTool>))]
[JsonSerializable(typeof(McpResponse))]
[JsonSerializable(typeof(McpContent))]
[JsonSerializable(typeof(GraphValidationRequest))]
[JsonSerializable(typeof(NodeRequest))]
[JsonSerializable(typeof(ConnectionRequest))]
[JsonSerializable(typeof(ExecutionItem))]
[JsonSerializable(typeof(ExecutionDelta))]
[JsonSerializable(typeof(ResourceMetrics))]
[JsonSerializable(typeof(TenantCostReport))]
[JsonSerializable(typeof(UsageEntry))]
[JsonSerializable(typeof(GraphDefinition))]
[JsonSerializable(typeof(NodeDefinition))]
[JsonSerializable(typeof(ConnectionDefinition))]
[JsonSerializable(typeof(ExecuteRequest))]
[JsonSerializable(typeof(IReadOnlyList<IReadOnlyList<ExecutionItem>>))]
[JsonSerializable(typeof(IDictionary<string, object>))]
[JsonSerializable(typeof(IDictionary<string, object?>))]
[JsonSerializable(typeof(Dictionary<string, object?>))]
[JsonSerializable(typeof(NodePackage))]
[JsonSerializable(typeof(JsonElement))]
[JsonSerializable(typeof(McpToolCallRequest))]
[JsonSerializable(typeof(McpToolCallResponse))]
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, WriteIndented = false)]
public partial class AgentFlowJsonContext : JsonSerializerContext
{
}
