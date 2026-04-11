using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AgentFlow.Backend.Core.Nodes;
using AgentFlow.Backend.Core.Execution;
using Microsoft.Extensions.Logging;

namespace AgentFlow.Backend.Nodes.Data;

public sealed class ItemListNode : INodeHandler
{
    private readonly ILogger<ItemListNode> _log;
    public string NodeId { get; }

    public ItemListNode(string nodeId, ILogger<ItemListNode> log)
    {
        NodeId = nodeId;
        _log   = log;
    }

    public async ValueTask<NodeResult> HandleAsync(NodeContext ctx, CancellationToken ct)
    {
        var operation = ctx.GetConfig<string>(NodeId, "operation", "filter");
        var items     = ctx.InputItems.ToList();

        IReadOnlyList<ExecutionItem> result = operation switch
        {
            "sort"        => Sort(items, ctx),
            "filter"      => Filter(items, ctx),
            "aggregate"   => Aggregate(items, ctx),
            "deduplicate" => Deduplicate(items, ctx),
            "batch"       => Batch(items, ctx),
            _             => throw new InvalidOperationException($"Unknown item-list operation: {operation}")
        };

        await Task.CompletedTask;
        _log.LogInformation("[ItemListNode] Op={Op} In={In} Out={Out}", operation, items.Count, result.Count);
        return NodeResult.Ok(new List<IReadOnlyList<ExecutionItem>> { result });
    }

    private IReadOnlyList<ExecutionItem> Sort(List<ExecutionItem> items, NodeContext ctx)
    {
        var field = ctx.GetConfig<string>(NodeId, "sort_field", "id");
        var desc  = ctx.GetConfig<bool>(NodeId, "sort_desc", false);
        return desc
            ? items.OrderByDescending(i => GetField(i, field)).ToList()
            : items.OrderBy(i => GetField(i, field)).ToList();
    }

    private IReadOnlyList<ExecutionItem> Filter(List<ExecutionItem> items, NodeContext ctx)
    {
        var field = ctx.GetConfig<string>(NodeId, "filter_field", "");
        var value = ctx.GetConfig<string>(NodeId, "filter_value", "");
        if (string.IsNullOrEmpty(field)) return items;
        return items.Where(i => string.Equals(GetField(i, field)?.ToString(), value, StringComparison.OrdinalIgnoreCase)).ToList();
    }

    private IReadOnlyList<ExecutionItem> Aggregate(List<ExecutionItem> items, NodeContext ctx)
    {
        var field = ctx.GetConfig<string>(NodeId, "aggregate_field", "value");
        var op    = ctx.GetConfig<string>(NodeId, "aggregate_op", "sum");

        var nums  = items.Select(i => double.TryParse(GetField(i, field)?.ToString(), out var d) ? d : 0.0).ToList();
        double agg = op switch
        {
            "sum"   => nums.Sum(),
            "avg"   => nums.Count > 0 ? nums.Average() : 0,
            "min"   => nums.Count > 0 ? nums.Min() : 0,
            "max"   => nums.Count > 0 ? nums.Max() : 0,
            "count" => items.Count,
            _       => nums.Sum()
        };
        return new List<ExecutionItem> { new ExecutionItem(new Dictionary<string, object?> { [field] = agg, ["operation"] = op }) };
    }

    private IReadOnlyList<ExecutionItem> Deduplicate(List<ExecutionItem> items, NodeContext ctx)
    {
        var field = ctx.GetConfig<string>(NodeId, "key_field", "id");
        var seen  = new HashSet<string>();
        return items.Where(i => seen.Add(GetField(i, field)?.ToString() ?? "")).ToList();
    }

    private IReadOnlyList<ExecutionItem> Batch(List<ExecutionItem> items, NodeContext ctx)
    {
        var size    = ctx.GetConfig<int>(NodeId, "batch_size", 10);
        var batches = new List<ExecutionItem>();
        for (int i = 0; i < items.Count; i += size)
        {
            var batch = items.Skip(i).Take(size).Select(x => x.Data).ToList();
            batches.Add(new ExecutionItem(new Dictionary<string, object?> { ["batch"] = batch, ["batch_index"] = i / size }));
        }
        return batches;
    }

    private static object? GetField(ExecutionItem item, string field)
        => item.Data.TryGetValue(field, out var v) ? v : null;
}
