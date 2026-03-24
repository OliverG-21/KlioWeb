namespace Klio.Core.Interfaces;

public interface IProcessingQueue
{
    void Enqueue(string filePath, int? topicId = null);
    Task<(string FilePath, int? TopicId)> DequeueAsync(CancellationToken cancellationToken);
    int Count { get; }
}
