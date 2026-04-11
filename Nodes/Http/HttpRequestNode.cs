// Original Path: Nodes/Http/HttpRequestNode.cs
// Line in File: 7869
using System.Net.Http.Json;

namespace AgentFlow.Backend.Nodes.Http;

public sealed class HttpRequestNode : BaseNode
{
    private readonly IHttpClientFactory _httpFactory;
    public HttpRequestNode(string nodeId, IHttpClientFactory http, ILogger<BaseNode> log, IExecutionPolicy pol, IAuditLogger audit)
        : base(nodeId, log, pol, audit) => _httpFactory = http;

    public override async ValueTask<IReadOnlyList<IReadOnlyList<ExecutionItem>>> ExecuteAsync(NodeContext ctx, CancellationToken ct)
    {
        var outputItems = new List<ExecutionItem>();
        var method = ctx.GetConfig<string>(NodeId, "method", "GET");
        var baseUrl = ctx.GetConfig<string>(NodeId, "url", "");
        var client = _httpFactory.CreateClient("agentflow-http");

        foreach (var item in ctx.InputItems)
        {
            // Simple placeholder for expression resolution: replaces {{key}} with item.Data[key]
            var url = baseUrl;
            foreach (var kvp in item.Data) {
                url = url.Replace($"{{{{{kvp.Key}}}}}", kvp.Value?.ToString());
            }

            var req = new HttpRequestMessage(new HttpMethod(method), url);
            var resp = await client.SendAsync(req, ct);
            resp.EnsureSuccessStatusCode();
            
            var content = await resp.Content.ReadAsStringAsync(ct);
            var data = new Dictionary<string, object?> {
                ["body"] = content,
                ["status"] = (int)resp.StatusCode,
                ["headers"] = resp.Headers.ToDictionary(h => h.Key, h => (object)h.Value)
            };
            
            outputItems.Add(new ExecutionItem(data, PairedItem: item));
        }

        return new List<List<ExecutionItem>> { outputItems };
    }
}