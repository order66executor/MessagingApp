using System.Collections.Concurrent;

using Messaging.Shared.Models;

namespace Messaging.Shared.Services;

// Helper class for tracking ACKs. 
public class PendingAckTracker {
    private readonly ConcurrentDictionary<(long Id, StringIdentifier Target), TaskCompletionSource<bool>> pendingMessages; // Complex key!!
    private readonly CancellationToken ct;

    private static readonly TimeSpan waitLength = TimeSpan.FromSeconds(30); // Timeout length

    public PendingAckTracker(CancellationToken ct) {
        pendingMessages = new();
        this.ct = ct;
    }

    //waits for waitLength time for someone to call complete on the key
    public async Task<bool> RegisterWaitAsync((long Id, StringIdentifier UserId) key) {
        // create tcs to complete
        TaskCompletionSource<bool> tcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
        pendingMessages.TryAdd(key, tcs);
        
        // wait for waitLength or task completion through Complete
        Task result = await Task.WhenAny(tcs.Task, Task.Delay(waitLength, ct));

        if (result != tcs.Task)
            tcs.TrySetResult(false);

        pendingMessages.TryRemove(key, out _);

        // return if task completed (ack arrives) or timed out
        return await tcs.Task;
    }

    // complete waiting for key
    public void Complete((long Id, StringIdentifier UserId) key) {
        if (!pendingMessages.TryGetValue(key, out var result))
            Console.WriteLine($"Ack wait cannot be completed, no such dict entry {key.UserId}");
        
        if (result is null) return;

        result.TrySetResult(true);
    }


}