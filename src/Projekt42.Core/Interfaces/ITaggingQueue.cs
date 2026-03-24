namespace Projekt42.Core.Interfaces;

public interface ITaggingQueue
{
    void Enqueue(int imageId);
    ValueTask<int> DequeueAsync(CancellationToken ct);
    int Count { get; }
}
