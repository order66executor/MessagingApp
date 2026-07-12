using System.Collections.Concurrent;

namespace Messaging.Shared;

//FIFO type 
public class MessageDataBuffer : IDisposable {

    public int Count => queue.Count;
    private readonly ConcurrentQueue<MessageData> queue = [ ];

    private readonly AutoResetEvent hasMessage = new(false);

    public void Notify() {
        hasMessage.Set();
    }

    public void Enqueue(MessageData data) {
        queue.Enqueue(data);
    }

    public bool TryDequeue(out MessageData result) {
        return queue.TryDequeue(out result);
    }

    public void Dispose() {
        hasMessage.Dispose();
    }

}