using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using AgentFlow.Backend.Core.Triggers;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace AgentFlow.Backend.Core.Triggers;

public static class WebhookIngressExtensions
{
    public static IEndpointConventionBuilder MapWebhookIngress(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapPost("/webhook/{path}", async (string path, HttpContext http, WebhookIngress ingress) =>
        {
            var body = await http.Request.ReadFromJsonAsync<Dictionary<string, object?>>() ?? new();
            var correlationId = await ingress.HandleRequestAsync(path, body, http.RequestAborted);
            return Results.Accepted($"/api/executions/{correlationId}", new { correlationId });
        });
    }
}
