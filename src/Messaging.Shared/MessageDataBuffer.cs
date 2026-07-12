using System.Collections.Concurrent;

namespace Messaging.Shared;

//FIFO type 
public class MessageDataBuffer : IDisposable {

    public int Count => queue.Count;
    private readonly ConcurrentQueue<MessageData> queue = [ ];

    public AutoResetEvent HasMessage { get; } = new(false);

    public ManualResetEvent CanDispose { get; } = new(true);


    public void Enqueue(MessageData data) {
        queue.Enqueue(data);
        HasMessage.Set();
        CanDispose.Reset();
    }

    public bool TryDequeue(out MessageData? result) {
        if (queue.TryDequeue(out result)) {
            if (Count == 0) CanDispose.Set();
            return true;
        }

        else return false;
    }

    public void Dispose() {
        HasMessage.Dispose();
        CanDispose.Dispose();
    }

}