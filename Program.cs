using AgentFlow.Backend.Core.Nodes;
using AgentFlow.Backend.Core.Triggers;
using AgentFlow.Backend.Core.Scheduling;
using AgentFlow.Backend.Core.Policy;
using AgentFlow.Backend.Core.Serialization;
using AgentFlow.Backend.Core.Observability;
using AgentFlow.Backend.Core.Execution;
using AgentFlow.Backend.Core.Tenancy;
using AgentFlow.Backend.Core.Security;
using AgentFlow.Backend.Core.Graph;
using AgentFlow.Backend.Core.AI;
using AgentFlow.Backend.Core.Storage.Supabase;
using AgentFlow.Backend.Mcp;
using AgentFlow.Backend.Memory;
using AgentFlow.Backend.Sandbox;
using AgentFlow.Backend.Git;
using AgentFlow.Backend.Marketplace;
using AgentFlow.Backend.RealTime;
using AgentFlow.Backend.Core.State;
using AgentFlow.Backend.Core.Storage;
using AgentFlow.Backend.Api;
using StackExchange.Redis;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Embeddings;
using Microsoft.SemanticKernel.Connectors.AzureOpenAI;
using Serilog;
using OpenTelemetry;
using OpenTelemetry.Trace;
using OpenTelemetry.Metrics;
using FluentValidation;

var builder = WebApplication.CreateBuilder(args);

Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .WriteTo.Console()
    .Enrich.FromLogContext()
    .CreateLogger();
builder.Host.UseSerilog();

builder.Services.AddOpenTelemetry()
    .WithTracing(t => t.AddSource("AgentFlow.Backend"));

// Core policies and observability
builder.Services.AddSingleton<IExecutionPolicy, ExecutionPolicy>();
builder.Services.AddSingleton<IAuditLogger, OpenTelemetryAuditLogger>();
builder.Services.AddSingleton<INodePolicyEngine, NodePolicyEngine>();
builder.Services.AddSingleton<ITenantRouter, TenantRouter>();
builder.Services.AddSingleton<IGraphValidator, GraphValidator>();

// Semantic Kernel + AI
builder.Services.AddSingleton<Kernel>(sp =>
    Kernel.CreateBuilder()
        .AddAzureOpenAIChatCompletion(
            builder.Configuration["AzureOpenAI:ChatDeployment"] ?? "gpt-4o",
            builder.Configuration["AzureOpenAI:Endpoint"]!,
            builder.Configuration["AzureOpenAI:Key"]!)
        .Build());

builder.Services.AddSingleton<Microsoft.SemanticKernel.Embeddings.ITextEmbeddingGenerationService>(sp =>
    new AzureOpenAITextEmbeddingGenerationService(
        builder.Configuration["AzureOpenAI:EmbeddingsDeployment"] ?? "text-embedding-3-small",
        builder.Configuration["AzureOpenAI:Endpoint"]!,
        builder.Configuration["AzureOpenAI:Key"]!));

builder.Services.AddSingleton<INaturalLanguageCompiler, NaturalLanguageCompiler>();

// Vector memory
builder.Services.AddSingleton<IQdrantClient>(sp =>
    new QdrantVectorClient(
        builder.Configuration["Qdrant:Endpoint"] ?? "http://localhost:6333",
        builder.Configuration["Qdrant:ApiKey"],
        builder.Configuration["Qdrant:Collection"] ?? "agentflow-patterns",
        sp.GetRequiredService<Microsoft.SemanticKernel.Embeddings.ITextEmbeddingGenerationService>(),
        sp.GetRequiredService<ILogger<QdrantVectorClient>>()));

// Sandbox
builder.Services.AddSingleton<WasmSandbox>(sp => new WasmSandbox(new(256, 30000, true)));
var redisConn = builder.Configuration["Redis:ConnectionString"];
if (!string.IsNullOrEmpty(redisConn))
{
    builder.Services.AddSingleton<IConnectionMultiplexer>(ConnectionMultiplexer.Connect(redisConn));
    builder.Services.AddSingleton<IExecutionStateStore, RedisStateStore>();
    builder.Services.AddSingleton<IExecutionStateManager, RedisExecutionStateManager>();
    builder.Services.AddSingleton<AgentFlow.Backend.Core.Reliability.IDistributedLockManager, AgentFlow.Backend.Core.Reliability.RedisLockManager>();
}
else
{
    builder.Services.AddSingleton<IExecutionStateStore, InMemoryExecutionStateStore>();
    builder.Services.AddSingleton<IExecutionStateManager, InMemoryExecutionStateManager>();
}

// Security
builder.Services.AddSingleton<ISecretManager, VaultSecretManager>();
builder.Services.AddSingleton<IEncryptionService, EncryptionService>();
builder.Services.AddSingleton<ICredentialsStore, CredentialsStore>();

// Storage & Supabase
builder.Services.AddSingleton<ISupabaseClientFactory, SupabaseClientFactory>();
builder.Services.AddSingleton<IGraphStore, SupabaseGraphStore>();
builder.Services.AddSingleton<IExecutionLogger, SupabaseExecutionLogger>();

// Binary Storage
builder.Services.AddSingleton<IBinaryDataStore>(sp =>
    new LocalBinaryDataStore(
        Path.Combine(builder.Environment.ContentRootPath, "binary_data"),
        sp.GetRequiredService<ILogger<LocalBinaryDataStore>>()));

// Cost tracking (explicitly qualified to avoid ambiguity)
builder.Services.AddSingleton<AgentFlow.Backend.Core.Observability.ICostTracker, AgentFlow.Backend.Core.Observability.ExecutionCostTracker>();

// Reliability — Dead Letter Queue (Redis if available, else in-memory)
if (!string.IsNullOrEmpty(redisConn))
{
    builder.Services.AddSingleton<AgentFlow.Backend.Core.Reliability.IDeadLetterQueue, AgentFlow.Backend.Core.Reliability.RedisDlq>();
    builder.Services.AddSingleton<AgentFlow.Backend.Core.Reliability.IIdempotencyService, AgentFlow.Backend.Core.Reliability.RedisIdempotencyService>();
}
else
{
    builder.Services.AddSingleton<AgentFlow.Backend.Core.Reliability.IDeadLetterQueue, AgentFlow.Backend.Core.Reliability.InMemoryDlq>();
    builder.Services.AddSingleton<AgentFlow.Backend.Core.Reliability.IIdempotencyService, AgentFlow.Backend.Core.Reliability.InMemoryIdempotencyService>();
}

// WASM capability policy
builder.Services.AddSingleton<AgentFlow.Backend.Sandbox.WasmCapabilityPolicy>();

// AI Copilot
builder.Services.AddSingleton<AgentFlow.Backend.Core.AI.IAiCopilotService, AgentFlow.Backend.Core.AI.AiCopilotService>();

// Dynamic Node Discovery & MCP Infrastructure
builder.Services.AddSingleton<NodeDiscoveryService>();
builder.Services.AddSingleton<AgentFlow.Backend.Mcp.IMcpMetadataCache, AgentFlow.Backend.Mcp.SupabaseMcpMetadataCache>();
builder.Services.AddSingleton<AgentFlow.Backend.Mcp.McpAutoRegistrar>();
builder.Services.AddSingleton<AgentFlow.Backend.Sandbox.WasmModuleCache>(sp => 
    new AgentFlow.Backend.Sandbox.WasmModuleCache(Path.Combine(builder.Environment.ContentRootPath, "wasm_cache")));

// Initial Node Registration (Populate Discovery Service)
builder.Services.AddHostedService(sp => {
    var discovery = sp.GetRequiredService<NodeDiscoveryService>();
    var registrar = sp.GetRequiredService<McpAutoRegistrar>();
    
    // Native Primaries
    discovery.RegisterNode(new AgentFlow.Backend.Nodes.Triggers.WebhookTriggerNode(null!, null!, null!));
    discovery.RegisterNode(new AgentFlow.Backend.Nodes.Triggers.ScheduleTriggerNode(null!, null!, null!));
    discovery.RegisterNode(new AgentFlow.Backend.Nodes.Logic.ConditionNode(null!, null!, null!));
    discovery.RegisterNode(new AgentFlow.Backend.Nodes.Logic.LoopNode(null!, null!, null!));
    discovery.RegisterNode(new AgentFlow.Backend.Nodes.Data.MergeNode(null!, null!, null!));
    discovery.RegisterNode(new AgentFlow.Backend.Nodes.Data.StreamJsonNode(null!, null!, null!));
    discovery.RegisterNode(new AgentFlow.Backend.Sandbox.WasmCodeNode(null!, null!, null!));
    
    // Start dynamic discovery
    _ = registrar.RegisterAllAsync(builder.Services, CancellationToken.None);
    
    return null!; // Placeholder for background service if needed
});

// Triggers & Engines
builder.Services.AddSingleton<AgentFlow.Backend.Core.Triggers.WebhookIngress>();
builder.Services.AddSingleton<AgentFlow.Backend.Core.Scheduling.CronEngine>();

// Node Handlers (Native Templates)
builder.Services.AddKeyedSingleton<INodeHandler, AgentFlow.Backend.Nodes.Triggers.WebhookTriggerNode>("webhook-trigger");
builder.Services.AddKeyedSingleton<INodeHandler, AgentFlow.Backend.Nodes.Triggers.ScheduleTriggerNode>("schedule-trigger");
builder.Services.AddKeyedSingleton<INodeHandler, AgentFlow.Backend.Nodes.Logic.ConditionNode>("condition");
builder.Services.AddKeyedSingleton<INodeHandler, AgentFlow.Backend.Nodes.Logic.LoopNode>("loop");
builder.Services.AddKeyedSingleton<INodeHandler, AgentFlow.Backend.Nodes.Data.MergeNode>("merge");
builder.Services.AddKeyedSingleton<INodeHandler, AgentFlow.Backend.Nodes.Data.StreamJsonNode>("stream-json");
builder.Services.AddKeyedSingleton<INodeHandler, AgentFlow.Backend.Nodes.Data.StreamCsvNode>("stream-csv");
builder.Services.AddKeyedSingleton<INodeHandler, AgentFlow.Backend.Sandbox.WasmCodeNode>("wasm-code");

// Node Handlers (Scripting)
builder.Services.AddKeyedSingleton<INodeHandler, AgentFlow.Backend.Nodes.Scripting.JavaScriptNode>("javascript");
builder.Services.AddKeyedSingleton<INodeHandler, AgentFlow.Backend.Nodes.Scripting.PythonNode>("python");

// Node Handlers (Integrations)
builder.Services.AddKeyedSingleton<INodeHandler, AgentFlow.Backend.Nodes.Integrations.SupabaseNode>("supabase");
builder.Services.AddKeyedSingleton<INodeHandler, AgentFlow.Backend.Nodes.Integrations.AgentAiNode>("agent-ai");
builder.Services.AddKeyedSingleton<INodeHandler, AgentFlow.Backend.Nodes.Integrations.WebScraperNode>("web-scraper");
builder.Services.AddKeyedSingleton<INodeHandler, AgentFlow.Backend.Nodes.Integrations.GoogleSuiteNode>("google-suite");

// Management & Observability
builder.Services.AddSingleton<AgentFlow.Backend.Core.State.ExecutionSnapshotter>();

// Core engine
builder.Services.AddSingleton<ExecutionEngine>();
builder.Services.AddSingleton<ExecutionStreamRegistry>();

// Real-time streaming
builder.Services.AddSingleton<ExecutionStreamService>();

// Git and marketplace
builder.Services.AddSingleton<GraphGitSync>();
builder.Services.AddSingleton<MarketplaceRegistry>();

// HTTP client with resilience
builder.Services.AddHttpClient("agentflow-default", c => c.Timeout = TimeSpan.FromMinutes(5))
    .AddStandardResilienceHandler();

builder.Services.AddHttpClient("vault", c => c.Timeout = TimeSpan.FromSeconds(10))
    .AddStandardResilienceHandler();

// Triggers and scheduling
builder.Services.AddSingleton<AgentFlow.Backend.Core.Triggers.ITriggerDispatcher, AgentFlow.Backend.Core.Triggers.DefaultTriggerDispatcher>();
builder.Services.AddCronEngine();

// SignalR + Controllers
builder.Services.AddSignalR();
builder.Services.AddControllers()
    .AddJsonOptions(options => options.JsonSerializerOptions.TypeInfoResolverChain.Insert(0, AgentFlowJsonContext.Default));

var app = builder.Build();

app.UseSerilogRequestLogging();
app.UseRouting();

app.MapControllers();
app.MapHub<ExecutionHub>("/hub/execution");

app.MapPost("/deploy", async (AgentFlow.Backend.Core.Graph.GraphDefinition g, AgentFlow.Backend.Api.DeployOptions opts, AgentFlow.Backend.Git.GraphGitSync git, CancellationToken ct) =>
    Results.Ok(await git.DeployAsync(g, opts, ct)));

app.MapGet("/api/v1/marketplace/nodes", async (MarketplaceRegistry reg, CancellationToken ct) =>
    Results.Ok(await reg.GetIndexAsync(ct)));

app.MapGet("/marketplace/{id}/{ver}", async (string id, string ver, MarketplaceRegistry reg, CancellationToken ct) =>
    Results.Ok(await reg.DownloadAndVerifyAsync(id, ver, ct)));

app.MapGet("/health", () =>
    Results.Ok(new { status = "healthy", mode = "native-aot", version = "1.0.0", timestamp = DateTimeOffset.UtcNow }));

app.MapPost("/compile", async (CompileRequest req, INaturalLanguageCompiler compiler, CancellationToken ct) =>
{
    if (string.IsNullOrWhiteSpace(req.Description)) return Results.BadRequest("Description is required.");
    var graph = await compiler.CompileAsync(req.Description, ct);
    return Results.Ok(graph);
});

app.MapWebhookIngress();

app.Run();
