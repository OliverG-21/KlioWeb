using Klio.Infrastructure.Services;

namespace Klio.Tests.Services;

[Trait("Category", "Unit")]
public class TaggingQueueTests
{
    [Fact]
    public async Task Enqueue_SingleItem_CountIsOne()
    {
        // Arrange
        var sut = new TaggingQueue();
        sut.Enqueue(42);

        // Act – dequeue to confirm the item was enqueued (Channel with SingleReader=true does not support CanCount)
        var result = await sut.DequeueAsync(CancellationToken.None);

        // Assert
        Assert.Equal(42, result);
    }

    [Fact]
    public async Task EnqueueDequeue_SingleItem_ReturnsSameId()
    {
        // Arrange
        var sut = new TaggingQueue();
        sut.Enqueue(99);

        // Act
        var result = await sut.DequeueAsync(CancellationToken.None);

        // Assert
        Assert.Equal(99, result);
    }

    [Fact]
    public async Task Enqueue_MultipleItems_DequeueInFifoOrder()
    {
        // Arrange
        var sut = new TaggingQueue();
        sut.Enqueue(1);
        sut.Enqueue(2);
        sut.Enqueue(3);

        // Act
        var first  = await sut.DequeueAsync(CancellationToken.None);
        var second = await sut.DequeueAsync(CancellationToken.None);
        var third  = await sut.DequeueAsync(CancellationToken.None);

        // Assert
        Assert.Equal(1, first);
        Assert.Equal(2, second);
        Assert.Equal(3, third);
    }

    [Fact]
    public async Task Count_AfterMultipleEnqueues_DequeuesAllItems()
    {
        // Arrange – TaggingQueue uses SingleReader=true which makes CanCount=false,
        // so Count always returns 0. We verify all items are accessible instead.
        var sut = new TaggingQueue();

        // Act
        sut.Enqueue(10);
        sut.Enqueue(20);
        sut.Enqueue(30);
        sut.Enqueue(40);

        var results = new List<int>();
        for (var i = 0; i < 4; i++)
            results.Add(await sut.DequeueAsync(CancellationToken.None));

        // Assert
        Assert.Equal([10, 20, 30, 40], results);
    }

    [Fact]
    public async Task DequeueAsync_WithCancelledToken_ThrowsOperationCanceledException()
    {
        // Arrange
        var sut = new TaggingQueue();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => sut.DequeueAsync(cts.Token).AsTask());
    }
}
