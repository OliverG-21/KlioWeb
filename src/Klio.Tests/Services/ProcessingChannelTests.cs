using Klio.Infrastructure.Services;

namespace Klio.Tests.Services;

[Trait("Category", "Unit")]
public class ProcessingChannelTests
{
    [Fact]
    public void Enqueue_SingleItem_CountIsOne()
    {
        // Arrange
        var sut = new ProcessingChannel();

        // Act
        sut.Enqueue("/some/file.jpg");

        // Assert
        Assert.Equal(1, sut.Count);
    }

    [Fact]
    public async Task EnqueueDequeue_PreservesFilePathAndTopicId()
    {
        // Arrange
        var sut = new ProcessingChannel();
        sut.Enqueue("/photos/image.jpg", 7);

        // Act
        var (filePath, topicId) = await sut.DequeueAsync(CancellationToken.None);

        // Assert
        Assert.Equal("/photos/image.jpg", filePath);
        Assert.Equal(7, topicId);
    }

    [Fact]
    public async Task EnqueueDequeue_WithNullTopicId_PreservesNull()
    {
        // Arrange
        var sut = new ProcessingChannel();
        sut.Enqueue("/photos/image.jpg", null);

        // Act
        var (filePath, topicId) = await sut.DequeueAsync(CancellationToken.None);

        // Assert
        Assert.Equal("/photos/image.jpg", filePath);
        Assert.Null(topicId);
    }

    [Fact]
    public async Task Enqueue_MultipleItems_DequeueInFifoOrder()
    {
        // Arrange
        var sut = new ProcessingChannel();
        sut.Enqueue("/a.jpg", 1);
        sut.Enqueue("/b.jpg", 2);
        sut.Enqueue("/c.jpg", 3);

        // Act
        var first  = await sut.DequeueAsync(CancellationToken.None);
        var second = await sut.DequeueAsync(CancellationToken.None);
        var third  = await sut.DequeueAsync(CancellationToken.None);

        // Assert
        Assert.Equal("/a.jpg", first.FilePath);
        Assert.Equal(1, first.TopicId);
        Assert.Equal("/b.jpg", second.FilePath);
        Assert.Equal(2, second.TopicId);
        Assert.Equal("/c.jpg", third.FilePath);
        Assert.Equal(3, third.TopicId);
    }

    [Fact]
    public async Task DequeueAsync_WithCancelledToken_ThrowsOperationCanceledException()
    {
        // Arrange
        var sut = new ProcessingChannel();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => sut.DequeueAsync(cts.Token));
    }
}
