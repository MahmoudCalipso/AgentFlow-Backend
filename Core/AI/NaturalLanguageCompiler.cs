using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AgentFlow.Backend.Core.Graph;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;

namespace AgentFlow.Backend.Core.AI;

public interface INaturalLanguageCompiler
{
    Task<GraphDefinition> CompileAsync(string naturalLanguageDescription, CancellationToken ct);
}

public sealed class NaturalLanguageCompiler : INaturalLanguageCompiler
{
    private readonly Kernel _kernel;
    private readonly IGraphValidator _validator;
    private readonly ILogger<NaturalLanguageCompiler> _log;

    private const string SystemPrompt = @"
You are an AgentFlow graph compiler. Convert the user's natural language description into a valid AgentFlow graph JSON.
Available node types: webhook-trigger, http-request, condition, mcp-tool, wasm, agentic, merge, split, transform.

Output ONLY valid JSON in this exact structure:
{
  ""id"": ""<unique-id>"",
  ""name"": ""<descriptive-name>"",
  ""nodes"": [
    { ""id"": ""node1"", ""type"": ""webhook-trigger"", ""config"": {} }
  ],
  ""edges"": [
    { ""sourceNodeId"": ""node1"", ""sourcePort"": 0, ""targetNodeId"": ""node2"", ""targetPort"": 0 }
  ]
}";

    public NaturalLanguageCompiler(Kernel kernel, IGraphValidator validator, ILogger<NaturalLanguageCompiler> log)
    {
        _kernel = kernel;
        _validator = validator;
        _log = log;
    }

    public async Task<GraphDefinition> CompileAsync(string naturalLanguageDescription, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(naturalLanguageDescription))
            throw new ArgumentException("Description cannot be empty.", nameof(naturalLanguageDescription));

        _log.LogInformation("Compiling NL description to graph: {Description}", naturalLanguageDescription.Length > 100 ? naturalLanguageDescription[..100] + "..." : naturalLanguageDescription);

        var prompt = $"{SystemPrompt}\n\nUser request: {naturalLanguageDescription}\n\nJSON graph:";
        var result = await _kernel.InvokePromptAsync(prompt, cancellationToken: ct);
        var rawJson = result.GetValue<string>() ?? "{}";

        var cleanJson = StripJsonFences(rawJson);

        GraphDefinition graph;
        try
        {
            using var doc = JsonDocument.Parse(cleanJson);
            var root = doc.RootElement;

            var id = root.TryGetProperty("id", out var idProp) ? idProp.GetString() ?? Guid.NewGuid().ToString("N") : Guid.NewGuid().ToString("N");
            var name = root.TryGetProperty("name", out var nameProp) ? nameProp.GetString() ?? "Generated Graph" : "Generated Graph";

            var nodes = new System.Collections.Generic.List<NodeDef>();
            if (root.TryGetProperty("nodes", out var nodesArr))
            {
                foreach (var n in nodesArr.EnumerateArray())
                {
                    var nodeId = n.TryGetProperty("id", out var ni) ? ni.GetString() ?? Guid.NewGuid().ToString("N") : Guid.NewGuid().ToString("N");
                    var nodeType = n.TryGetProperty("type", out var nt) ? nt.GetString() ?? "transform" : "transform";
                    nodes.Add(new NodeDef(nodeId, nodeType));
                }
            }

            var edges = new System.Collections.Generic.List<EdgeDef>();
            if (root.TryGetProperty("edges", out var edgesArr))
            {
                foreach (var e in edgesArr.EnumerateArray())
                {
                    var src = e.TryGetProperty("sourceNodeId", out var sn) ? sn.GetString() ?? "" : "";
                    var sp = e.TryGetProperty("sourcePort", out var spv) ? spv.GetInt32() : 0;
                    var tgt = e.TryGetProperty("targetNodeId", out var tn) ? tn.GetString() ?? "" : "";
                    var tp = e.TryGetProperty("targetPort", out var tpv) ? tpv.GetInt32() : 0;
                    edges.Add(new EdgeDef(src, sp, tgt, tp));
                }
            }

            graph = new GraphDefinition(id, name, nodes, edges);
        }
        catch (JsonException ex)
        {
            _log.LogError(ex, "Failed to parse LLM-generated graph JSON");
            throw new InvalidOperationException("LLM returned invalid graph JSON. Please try again with a clearer description.", ex);
        }

        var validation = await _validator.ValidateAsync(graph, ct);
        if (!validation.IsValid)
        {
            _log.LogWarning("Generated graph has validation errors: {Errors}", string.Join("; ", validation.Errors));
            throw new InvalidOperationException($"Generated graph failed validation: {string.Join("; ", validation.Errors)}");
        }

        _log.LogInformation("Successfully compiled NL description into graph {GraphId} with {NodeCount} nodes", graph.Id, graph.Nodes.Count);
        return graph;
    }

    private static string StripJsonFences(string raw)
    {
        var trimmed = raw.Trim();
        if (trimmed.StartsWith("```json", StringComparison.OrdinalIgnoreCase))
            trimmed = trimmed[7..];
        else if (trimmed.StartsWith("```"))
            trimmed = trimmed[3..];
        if (trimmed.EndsWith("```"))
            trimmed = trimmed[..^3];
        return trimmed.Trim();
    }
}
