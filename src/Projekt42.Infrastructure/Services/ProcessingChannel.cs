using System.Threading.Channels;
using Projekt42.Core.Interfaces;

namespace Projekt42.Infrastructure.Services;

public class ProcessingChannel : IProcessingQueue
{
    private readonly Channel<(string FilePath, int? TopicId)> _channel;

    public ProcessingChannel()
    {
        _channel = Channel.CreateUnbounded<(string, int?)>(new UnboundedChannelOptions
        {
            SingleReader = false,
            SingleWriter = false
        });
    }

    public void Enqueue(string filePath, int? topicId = null)
    {
        _channel.Writer.TryWrite((filePath, topicId));
    }

    public async Task<(string FilePath, int? TopicId)> DequeueAsync(CancellationToken cancellationToken)
    {
        return await _channel.Reader.ReadAsync(cancellationToken);
    }

    public int Count => (int)(_channel.Reader.CanCount ? _channel.Reader.Count : 0);
}
