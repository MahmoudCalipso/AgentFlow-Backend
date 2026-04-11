using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AgentFlow.Backend.Core.Execution;
using AgentFlow.Backend.Nodes.Data;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentFlow.Benchmarks;

public static class ThroughputBenchmark
{
    public static async Task RunAsync()
    {
        Console.WriteLine("🚀 Starting AgentFlow Throughput Benchmark...");
        
        var node = new StreamJsonNode("bench-node", NullLogger<StreamJsonNode>.Instance, null!, null!);
        var items = new List<ExecutionItem>();
        for (int i = 0; i < 1_000_000; i++)
        {
            items.Add(new ExecutionItem(new Dictionary<string, object?> { ["id"] = i, ["data"] = "test-payload" }));
        }

        var ctx = new NodeContext("bench-cor", "bench-graph", items, null!, null!, null!, CancellationToken.None);
        
        Console.WriteLine($"📦 Processing {items.Count:N0} items...");
        
        var sw = Stopwatch.StartNew();
        var result = await node.ExecuteAsync(ctx, CancellationToken.None);
        sw.Stop();

        var totalItems = result.Sum(l => l.Count);
        var itemsPerSec = totalItems / sw.Elapsed.TotalSeconds;

        Console.WriteLine("--------------------------------------------------");
        Console.WriteLine($"⏱️ Total Time: {sw.Elapsed.TotalSeconds:F2} seconds");
        Console.WriteLine($"📊 Throughput: {itemsPerSec:N0} rows/second");
        Console.WriteLine($"✅ Performance Goal (100k): {(itemsPerSec >= 100_000 ? "PASSED" : "FAILED")}");
        Console.WriteLine("--------------------------------------------------");
    }
}
