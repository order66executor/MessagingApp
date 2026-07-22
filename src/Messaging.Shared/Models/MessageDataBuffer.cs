using System.Collections.Concurrent;
using System.Threading.Channels;

namespace Messaging.Shared.Models;

//FIFO type 
public class MessageDataBuffer : IDisposable {
    private readonly Channel<MessageData> channel =
        Channel.CreateUnbounded<MessageData>(new UnboundedChannelOptions
        {
            SingleReader = true,   // set true if only one loop reads it
            SingleWriter = false
        });

    public ChannelWriter<MessageData> Writer => channel.Writer;
    public ChannelReader<MessageData> Reader => channel.Reader;

    public void Dispose() => Writer.TryComplete();
}
