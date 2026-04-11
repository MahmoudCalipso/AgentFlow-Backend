using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace AgentFlow.Backend.Core.Execution;

public sealed class NodeStream<T> : IAsyncDisposable
{
    private readonly Channel<T> _channel;
    private readonly string _streamId;
    private long _producedCount;
    private long _consumedCount;

    public string StreamId => _streamId;
    public long ProducedCount => Interlocked.Read(ref _producedCount);
    public long ConsumedCount => Interlocked.Read(ref _consumedCount);

    public NodeStream(string streamId, int capacity = 1024)
    {
        _streamId = streamId;
        _channel = Channel.CreateBounded<T>(new BoundedChannelOptions(capacity)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleWriter = false,
            SingleReader = false
        });
    }

    public async ValueTask WriteAsync(T item, CancellationToken ct = default)
    {
        await _channel.Writer.WriteAsync(item, ct);
        Interlocked.Increment(ref _producedCount);
    }

    public async ValueTask WriteBatchAsync(IEnumerable<T> items, CancellationToken ct = default)
    {
        foreach (var item in items)
        {
            await _channel.Writer.WriteAsync(item, ct);
            Interlocked.Increment(ref _producedCount);
        }
    }

    public void Complete(Exception? error = null)
    {
        _channel.Writer.Complete(error);
    }

    public async IAsyncEnumerable<T> ReadAllAsync([EnumeratorCancellation] CancellationToken ct = default)
    {
        await foreach (var item in _channel.Reader.ReadAllAsync(ct))
        {
            Interlocked.Increment(ref _consumedCount);
            yield return item;
        }
    }

    public async ValueTask<T?> TryReadAsync(CancellationToken ct = default)
    {
        if (_channel.Reader.TryRead(out var item))
        {
            Interlocked.Increment(ref _consumedCount);
            return item;
        }

        try
        {
            if (await _channel.Reader.WaitToReadAsync(ct))
            {
                if (_channel.Reader.TryRead(out item))
                {
                    Interlocked.Increment(ref _consumedCount);
                    return item;
                }
            }
        }
        catch (OperationCanceledException) { }

        return default;
    }

    public bool TryWrite(T item)
    {
        if (_channel.Writer.TryWrite(item))
        {
            Interlocked.Increment(ref _producedCount);
            return true;
        }
        return false;
    }

    public ChannelReader<T> Reader => _channel.Reader;

    public async ValueTask DisposeAsync()
    {
        _channel.Writer.TryComplete();
        await Task.CompletedTask;
    }
}

public sealed class ExecutionStreamRegistry
{
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, NodeStream<ExecutionItem>> _streams = new();

    public NodeStream<ExecutionItem> GetOrCreate(string correlationId, int capacity = 1024)
    {
        return _streams.GetOrAdd(correlationId, id => new NodeStream<ExecutionItem>(id, capacity));
    }

    public bool TryGet(string correlationId, out NodeStream<ExecutionItem>? stream)
    {
        return _streams.TryGetValue(correlationId, out stream);
    }

    public async ValueTask CompleteAsync(string correlationId, Exception? error = null)
    {
        if (_streams.TryRemove(correlationId, out var stream))
        {
            stream.Complete(error);
            await stream.DisposeAsync();
        }
    }
}
