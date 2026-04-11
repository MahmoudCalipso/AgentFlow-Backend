using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AgentFlow.Backend.Core.Execution;
using AgentFlow.Backend.Core.Nodes;
using AgentFlow.Backend.Core.Policy;
using AgentFlow.Backend.Core.Observability;
using Microsoft.Extensions.Logging;
using Microsoft.Playwright;

namespace AgentFlow.Backend.Nodes.Integrations;

public sealed class WebScraperNode : BaseNode
{
    public WebScraperNode(string nodeId, ILogger<BaseNode> log, IExecutionPolicy policy, IAuditLogger audit)
        : base(nodeId, log, policy, audit)
    {
    }

    public override async ValueTask<IReadOnlyList<IReadOnlyList<ExecutionItem>>> ExecuteAsync(NodeContext ctx, CancellationToken ct)
    {
        var url = ctx.GetConfig<string>(NodeId, "url", "");
        var selector = ctx.GetConfig<string>(NodeId, "selector", "body");
        var outputItems = new List<ExecutionItem>();

        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Headless = true });
        
        var page = await browser.NewPageAsync();
        Log.LogInformation("Scraping URL: {Url}", url);
        
        await page.GotoAsync(url);
        var content = await page.InnerTextAsync(selector);

        outputItems.Add(new ExecutionItem(new Dictionary<string, object?> { 
            ["url"] = url,
            ["content"] = content,
            ["scrapedAt"] = DateTime.UtcNow
        }));

        return new List<List<ExecutionItem>> { outputItems };
    }
}
