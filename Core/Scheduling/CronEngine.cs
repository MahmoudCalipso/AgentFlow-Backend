using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AgentFlow.Backend.Core.Execution;
using AgentFlow.Backend.Core.Storage;
using Microsoft.Extensions.Logging;
using Quartz;

namespace AgentFlow.Backend.Core.Scheduling;

public sealed class CronEngine
{
    private readonly ISchedulerFactory _schedulerFactory;
    private readonly IGraphStore _graphStore;
    private readonly ExecutionEngine _engine;
    private readonly IExecutionLogger _execLogger;
    private readonly ILogger<CronEngine> _log;

    public CronEngine(
        ISchedulerFactory schedulerFactory,
        IGraphStore graphStore,
        ExecutionEngine engine,
        IExecutionLogger execLogger,
        ILogger<CronEngine> log)
    {
        _schedulerFactory = schedulerFactory;
        _graphStore = graphStore;
        _engine = engine;
        _execLogger = execLogger;
        _log = log;
    }

    public async Task ScheduleGraphAsync(string graphId, string cronExpression, CancellationToken ct)
    {
        var scheduler = await _schedulerFactory.GetScheduler(ct);
        
        var job = JobBuilder.Create<GraphExecutionJob>()
            .WithIdentity($"job_{graphId}", "agentflow")
            .UsingJobData("graphId", graphId)
            .Build();

        var trigger = TriggerBuilder.Create()
            .WithIdentity($"trig_{graphId}", "agentflow")
            .WithCronSchedule(cronExpression)
            .Build();

        await scheduler.ScheduleJob(job, trigger, ct);
        _log.LogInformation("Scheduled graph {GraphId} with cron: {Cron}", graphId, cronExpression);
    }
}

public sealed class GraphExecutionJob : IJob
{
    private readonly ExecutionEngine _engine;
    private readonly IGraphStore _graphStore;
    private readonly IExecutionLogger _execLogger;
    private readonly ILogger<GraphExecutionJob> _log;

    public GraphExecutionJob(ExecutionEngine engine, IGraphStore graphStore, IExecutionLogger execLogger, ILogger<GraphExecutionJob> log)
    {
        _engine = engine;
        _graphStore = graphStore;
        _execLogger = execLogger;
        _log = log;
    }

    public async Task Execute(IJobExecutionContext context)
    {
        var graphId = context.MergedJobDataMap.GetString("graphId");
        if (string.IsNullOrEmpty(graphId)) return;

        var graph = await _graphStore.GetByIdAsync(graphId, context.CancellationToken);
        if (graph == null) return;

        var correlationId = Guid.NewGuid().ToString("N");
        await _execLogger.LogStartAsync(correlationId, graphId, context.CancellationToken);

        // Map and execute
        // ... (Runtime mapping as in WebhookIngress)
        _log.LogInformation("Scheduled execution triggered for graph {GraphId}", graphId);
    }
}
