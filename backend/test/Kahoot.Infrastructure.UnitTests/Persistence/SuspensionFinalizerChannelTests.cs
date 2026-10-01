namespace Kahoot.Infrastructure.UnitTests.Persistence;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Kahoot.Infrastructure.Persistence;
using Xunit;

public sealed class SuspensionFinalizerChannelTests
{
    private readonly SuspensionFinalizerChannel _channel = new();

    [Fact]
    public void NotifySuspension_DeliversHintsInOrderFromSingleProducer()
    {
        Guid id1 = Guid.NewGuid();
        Guid id2 = Guid.NewGuid();
        Guid id3 = Guid.NewGuid();

        _channel.NotifySuspension(id1);
        _channel.NotifySuspension(id2);
        _channel.NotifySuspension(id3);

        bool read1 = _channel.TryRead(out Guid result1);
        bool read2 = _channel.TryRead(out Guid result2);
        bool read3 = _channel.TryRead(out Guid result3);
        bool read4 = _channel.TryRead(out Guid _);

        Assert.True(read1);
        Assert.True(read2);
        Assert.True(read3);
        Assert.False(read4);

        Assert.Equal(id1, result1);
        Assert.Equal(id2, result2);
        Assert.Equal(id3, result3);
    }

    [Fact]
    public void NotifySuspension_PreservesRepeatedHints()
    {
        Guid repeatedId = Guid.NewGuid();

        _channel.NotifySuspension(repeatedId);
        _channel.NotifySuspension(repeatedId);

        bool read1 = _channel.TryRead(out Guid result1);
        bool read2 = _channel.TryRead(out Guid result2);
        bool read3 = _channel.TryRead(out Guid _);

        Assert.True(read1);
        Assert.True(read2);
        Assert.False(read3);

        Assert.Equal(repeatedId, result1);
        Assert.Equal(repeatedId, result2);
    }

    [Fact]
    public async Task ReadAsync_WakesAfterNotification()
    {
        Guid expectedId = Guid.NewGuid();
        ValueTask<Guid> readTask = _channel.ReadAsync(CancellationToken.None);

        _channel.NotifySuspension(expectedId);
        Guid actualId = await readTask;

        Assert.Equal(expectedId, actualId);
    }

    [Fact]
    public async Task WaitToReadAsync_SignalsWithoutConsumingHint()
    {
        Guid expectedId = Guid.NewGuid();
        ValueTask<bool> waitTask = _channel.WaitToReadAsync(CancellationToken.None);

        _channel.NotifySuspension(expectedId);
        bool canRead = await waitTask;

        Assert.True(canRead);

        bool tryReadResult = _channel.TryRead(out Guid readId);
        Assert.True(tryReadResult);
        Assert.Equal(expectedId, readId);
    }

    [Fact]
    public async Task EmptyChannel_ReadAndWaitHonorCancellation()
    {
        using CancellationTokenSource cts = new();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
        {
            await _channel.ReadAsync(cts.Token);
        });

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
        {
            await _channel.WaitToReadAsync(cts.Token);
        });
    }

    [Fact]
    public void NotifySuspension_DropsNewHintsAfterCapacity()
    {
        List<Guid> initialBatch = new(1024);
        for (int i = 0; i < 1024; i++)
        {
            Guid id = Guid.NewGuid();
            initialBatch.Add(id);
            _channel.NotifySuspension(id);
        }

        Guid droppedExtraId = Guid.NewGuid();
        _channel.NotifySuspension(droppedExtraId);

        List<Guid> drained = new(1024);
        while (_channel.TryRead(out Guid id))
        {
            drained.Add(id);
        }

        Assert.Equal(1024, drained.Count);
        Assert.Equal(initialBatch, drained);
        Assert.DoesNotContain(droppedExtraId, drained);
    }

    [Fact]
    public void NotifySuspension_AcceptsAgainAfterDrain()
    {
        List<Guid> initialBatch = new(1024);
        for (int i = 0; i < 1024; i++)
        {
            Guid id = Guid.NewGuid();
            initialBatch.Add(id);
            _channel.NotifySuspension(id);
        }

        bool freedOne = _channel.TryRead(out Guid firstRead);
        Assert.True(freedOne);
        Assert.Equal(initialBatch[0], firstRead);

        Guid newlyAcceptedId = Guid.NewGuid();
        _channel.NotifySuspension(newlyAcceptedId);

        List<Guid> remaining = new(1024);
        while (_channel.TryRead(out Guid id))
        {
            remaining.Add(id);
        }

        Assert.Equal(1024, remaining.Count);
        Assert.Equal(newlyAcceptedId, remaining.Last());
    }

    [Fact]
    public async Task NotifySuspension_MultipleProducersRetainHintsBelowCapacity()
    {
        const int producerCount = 8;
        const int itemsPerProducer = 50; // 400 total items, well below 1024 capacity
        List<Guid[]> producerItems = new();
        HashSet<Guid> allExpectedIds = new();

        for (int p = 0; p < producerCount; p++)
        {
            Guid[] items = new Guid[itemsPerProducer];
            for (int i = 0; i < itemsPerProducer; i++)
            {
                Guid id = Guid.NewGuid();
                items[i] = id;
                allExpectedIds.Add(id);
            }
            producerItems.Add(items);
        }

        Task[] producerTasks = new Task[producerCount];
        for (int p = 0; p < producerCount; p++)
        {
            Guid[] items = producerItems[p];
            producerTasks[p] = Task.Run(() =>
            {
                for (int i = 0; i < items.Length; i++)
                {
                    _channel.NotifySuspension(items[i]);
                }
            });
        }

        await Task.WhenAll(producerTasks);

        HashSet<Guid> receivedIds = new();
        while (_channel.TryRead(out Guid readId))
        {
            receivedIds.Add(readId);
        }

        Assert.Equal(400, receivedIds.Count);
        Assert.True(allExpectedIds.SetEquals(receivedIds));
    }
}
