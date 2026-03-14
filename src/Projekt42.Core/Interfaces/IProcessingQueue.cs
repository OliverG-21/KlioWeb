namespace Projekt42.Core.Interfaces;

public interface IProcessingQueue
{
    void Enqueue(string filePath);
    Task<string> DequeueAsync(CancellationToken cancellationToken);
    int Count { get; }
}
