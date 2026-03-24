using System.Threading.Channels;
using Projekt42.Core.Interfaces;

namespace Projekt42.Infrastructure.Services;

public class TaggingQueue : ITaggingQueue
{
    private readonly Channel<int> _channel = Channel.CreateUnbounded<int>(
        new UnboundedChannelOptions { SingleReader = true });

    public void Enqueue(int imageId) => _channel.Writer.TryWrite(imageId);

    public ValueTask<int> DequeueAsync(CancellationToken ct) =>
        _channel.Reader.ReadAsync(ct);

    public int Count => _channel.Reader.CanCount ? _channel.Reader.Count : 0;
}
