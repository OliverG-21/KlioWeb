using System.Threading.Channels;
using Projekt42.Core.Interfaces;

namespace Projekt42.Infrastructure.Services;

public class ProcessingChannel : IProcessingQueue
{
    private readonly Channel<string> _channel;

    public ProcessingChannel()
    {
        _channel = Channel.CreateUnbounded<string>(new UnboundedChannelOptions
        {
            SingleReader = false,
            SingleWriter = false
        });
    }

    public void Enqueue(string filePath)
    {
        _channel.Writer.TryWrite(filePath);
    }

    public async Task<string> DequeueAsync(CancellationToken cancellationToken)
    {
        return await _channel.Reader.ReadAsync(cancellationToken);
    }

    public int Count => (int)(_channel.Reader.CanCount ? _channel.Reader.Count : 0);
}
