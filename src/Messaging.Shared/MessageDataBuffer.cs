using System.Collections.Concurrent;

namespace Messaging.Shared;

//FIFO type 
public class MessageDataBuffer : IDisposable {

    public int Count { 
        get;
        set {
            field = value;
            if (field == 0) CanDispose.Set();
            else CanDispose.Reset();
        }
    }
    private readonly ConcurrentQueue<MessageData> queue = [ ];

    public AutoResetEvent HasMessage { get; } = new(false);

    public ManualResetEvent CanDispose { get; } = new(false);


    public void Enqueue(MessageData data) {
        queue.Enqueue(data);
        ++Count;
    }

    public bool TryDequeue(out MessageData result) {
        if (queue.TryDequeue(out result)) {
            --Count;
            return true;
        }

        else return false;
        

    }

    public void Dispose() {
        HasMessage.Dispose();
    }

}