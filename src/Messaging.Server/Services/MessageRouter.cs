using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Messaging.Server.Data;
using Messaging.Shared.Models;
using Messaging.Shared.Data;
using Messaging.Shared.Services;

namespace Messaging.Server.Services;

public class MessageRouter  {
    private readonly ConcurrentDictionary<StringIdentifier, MessageConnectionHandler> handlers;
    private readonly string dbPath;
    private static readonly TimeSpan sweepInterval = TimeSpan.FromSeconds(3);
    public AckWaitHandler AckHandler { get; }

    public MessageRouter(ConcurrentDictionary<StringIdentifier, MessageConnectionHandler> handlers, AckWaitHandler ackHandler, string dbPath = "messaging_server.db") {
        this.handlers = handlers;
        this.dbPath = dbPath;
        using (var db = CreateDbContext()) {
            DbUtil.DeleteDb(db);
            db.Database.EnsureCreated();
            db.Messages
                .Where(e => e.State == MessageState.Pending || e.State == MessageState.AutoPending)
                .ExecuteUpdate(e => e.SetProperty(x => x.State, MessageState.Unsent));
        }
        this.AckHandler = ackHandler;
    }

    private ServerDbContext CreateDbContext() => new(dbPath);

    public async Task<bool> UpdateHighestAckAsync(MessageData message) {
        var conversationKey = DbUtil.GetConversationKey(message.SourceId, message.TargetId);
        using var db = CreateDbContext();

        var existing = await db.HighestAcks
            .FirstOrDefaultAsync(e => e.ConversationKey == DbUtil.GetConversationKey(message.SourceId, message.TargetId) && e.SenderUsername == message.SourceId.Value);

        if (existing is null) {
            db.HighestAcks.Add(new() {
                ConversationKey = conversationKey,
                SenderUsername = message.SourceId.Value,
                HighestAck = message.Id
            });
        }
        else {
            if (existing.HighestAck < message.Id)
                existing.HighestAck = message.Id;
            else return false;
        }
        Console.WriteLine("Updating hightest ack");
        await db.SaveChangesAsync();
        return true;
    }

    private async Task SendWrapperAsync(MessageWrapper wrapper, ServerDbContext db) {
        MessageData? message = JsonSerializer.Deserialize<MessageData>(wrapper.SerializedMessageData);
        if (message is null) {
            Console.WriteLine("Message data was null when attempting to send during sweep");
            return;
        }
        if (handlers.TryGetValue(message.TargetId, out _)) {
            try {
                bool result = await AckHandler.EnqueueMessageAsync(message);
                
                if (result) {
                    db.Messages.Remove(wrapper);
                }

                else wrapper.State = MessageState.Unsent;
                await db.SaveChangesAsync();

                if (result) Console.WriteLine($"Message routed to {message.TargetId.Value}");
                else Console.WriteLine("no ack received");
        
            }
            catch (Exception e) {
                Console.WriteLine($"Failed to write to out buffer for {message.TargetId.Value}: {e.Message}");
            }
        }
        else {
            wrapper.State = MessageState.Unsent;
            await db.SaveChangesAsync();
            Console.WriteLine($"User {message.TargetId.Value} offline, message stored in DB");
        }

    }


    public async Task RouteMessageAsync(MessageData message) {
        string conversationKey = DbUtil.GetConversationKey(message.SourceId, message.TargetId);

        using var db = CreateDbContext();


        MessageWrapper wrapper = new() {
            ConversationKey = conversationKey,
            SequenceId = message.Id,
            SenderUsername = message.SourceId.Value,
            ReceiverUsername = message.TargetId.Value,
            SerializedMessageData = JsonSerializer.SerializeToUtf8Bytes(message),
            StoredAtUtc = DateTime.UtcNow,
            State = MessageState.Pending
        };

        db.Messages.Add(wrapper);
        try {
            await db.SaveChangesAsync();
        }
        catch (DbUpdateException) {
            Console.WriteLine("Server received duplicate message, discarded");
            return;
        }

        if (handlers.TryGetValue(message.TargetId, out MessageConnectionHandler? targetHandler)) {
            try {
                bool result = await AckHandler.EnqueueMessageAsync(message);
                
                if (result) {
                    db.Messages.Remove(wrapper);
                }

                else wrapper.State = MessageState.Unsent;
                await db.SaveChangesAsync();

                if (result) Console.WriteLine($"Message routed to {message.TargetId.Value}");
                else Console.WriteLine("no ack received");
        
            }
            catch (Exception e) {
                Console.WriteLine($"Failed to write to out buffer for {message.TargetId.Value}: {e.Message}");
            }
        }
        else {
            wrapper.State = MessageState.Unsent;
            await db.SaveChangesAsync();
            Console.WriteLine($"User {message.TargetId.Value} offline, message stored in DB");
        }
    }

    public async Task DeliverPendingMessagesAsync(StringIdentifier userId, MessageConnectionHandler handler) {
        using var db = CreateDbContext();

        await db.Messages
            .Where(m => m.ReceiverUsername == userId.Value && m.State == MessageState.Unsent)
            .ExecuteUpdateAsync(m => m.SetProperty(m => m.State, MessageState.Pending));

        var pendingMessages = await db.Messages
            .Where(m => m.ReceiverUsername == userId.Value && m.State == MessageState.Pending)
            .OrderBy(m => m.SequenceId)
            .ToListAsync();

        Console.WriteLine("Messages claimed");

        List<Task<bool>> sendTasks = [ ];

        var realPendingMessages = new List<MessageWrapper>();

        foreach (var wrapper in pendingMessages) {
            try {
                MessageData? messageData = JsonSerializer.Deserialize<MessageData>(wrapper.SerializedMessageData);
                if (messageData != null) {
                    sendTasks.Add(AckHandler.EnqueueMessageAsync(messageData));
                    Console.WriteLine("Pending message enqueued");
                    realPendingMessages.Add(wrapper);
                }
            }
            catch (Exception e) {
                Console.WriteLine($"Error delivering pending message to {userId.Value}: {e.Message}");
            }
        }

        bool[] results = await Task.WhenAll(sendTasks);
        int success = 0, failure = 0;

        for (int i = 0; i < sendTasks.Count; ++i) {
            if (results[i]) {
                ++success;
                db.Messages.Remove(realPendingMessages[i]);
            }
            else {
                realPendingMessages[i].State = MessageState.Unsent;
                ++failure;
            }
        }

        if (realPendingMessages.Count > 0) {
            await db.SaveChangesAsync();
            Console.WriteLine($"Delivered {success} pending messages to {userId.Value}, failed {failure}");
        }
    }

    public async Task StartUnsentSweepAsync(CancellationToken ct) {
        while (!ct.IsCancellationRequested) {
            Console.WriteLine("Sweeping...");
            using var db = CreateDbContext();

            var onlineUsers = handlers.Keys.Select(x => x.Value).ToArray();
            try {

                await db.Messages
                    .Where(m => m.State == MessageState.Unsent && onlineUsers.Contains(m.ReceiverUsername))
                    .ExecuteUpdateAsync(m => m.SetProperty(x => x.State, MessageState.AutoPending), ct);
                
                MessageWrapper[] unsent = await db.Messages
                    .Where(m => m.State == MessageState.AutoPending)
                    .OrderBy(m => m.SequenceId)
                    .ToArrayAsync(ct);

                foreach (MessageWrapper wrapper in unsent) {
                    await SendWrapperAsync(wrapper, db);
                }

                await Task.Delay(sweepInterval, ct);
            }
            catch (TaskCanceledException) {
                Console.WriteLine("Sweep canceled");
            }
            catch (OperationCanceledException) {
                Console.WriteLine("Sweep canceled");

            }
        }


    }


}
