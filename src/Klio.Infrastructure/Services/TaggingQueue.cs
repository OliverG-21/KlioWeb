using System.Threading.Channels;
using Klio.Core.Interfaces;

namespace Klio.Infrastructure.Services;

public class TaggingQueue : ITaggingQueue
{
    private readonly Channel<int> _channel = Channel.CreateUnbounded<int>(
        new UnboundedChannelOptions { SingleReader = true });

    public void Enqueue(int imageId) => _channel.Writer.TryWrite(imageId);

    public ValueTask<int> DequeueAsync(CancellationToken ct) =>
        _channel.Reader.ReadAsync(ct);

    public int Count => _channel.Reader.CanCount ? _channel.Reader.Count : 0;
}
