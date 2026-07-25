using System.Collections.Concurrent;

using Messaging.Shared.Models;

namespace Messaging.Shared.Services;

public class PendingAckTracker {
    private readonly ConcurrentDictionary<(long Id, StringIdentifier Target), TaskCompletionSource<bool>> pendingMessages;
    private readonly CancellationToken ct;

    private static readonly TimeSpan waitLength = TimeSpan.FromSeconds(30);

    public PendingAckTracker(CancellationToken ct) {
        pendingMessages = new();
        this.ct = ct;
    }

    //waits for waitLength for someone to call complete on the key
    public async Task<bool> RegisterWaitAsync((long Id, StringIdentifier Target) key) {
        // create tcs to complete
        TaskCompletionSource<bool> tcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
        pendingMessages.TryAdd(key, tcs);
        
        //wait for waitLength or task completion
        Task result = await Task.WhenAny(tcs.Task, Task.Delay(waitLength, ct));
        if (result != tcs.Task)
            tcs.TrySetResult(false);
        pendingMessages.TryRemove(key, out _);

        //return if task completed or timed out
        return await tcs.Task;
    }

    //complete waiting for key
    public void Complete((long Id, StringIdentifier Target) key) {
        if(!pendingMessages.TryGetValue(key, out var result))
            Console.WriteLine($"Ack wait cannot be completed, no such dict entry {key.Target}");
        
        if (result is null) return;
        result.TrySetResult(true);
    }


}