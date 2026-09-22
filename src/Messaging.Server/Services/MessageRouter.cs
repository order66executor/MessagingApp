using System.Collections.Concurrent;
using MessagePack;
using Microsoft.EntityFrameworkCore;
using Messaging.Server.Data;
using Messaging.Shared.Models;
using Messaging.Shared.Data;
using Messaging.Shared.Services;

namespace Messaging.Server.Services;

public class MessageRouter  {
    private readonly ConcurrentDictionary<StringIdentifier, MessageConnectionHandler> handlers;
    private readonly string dbPath;
    private static readonly TimeSpan sweepInterval = TimeSpan.FromSeconds(10); // Interval between unsent message sweeps
    public AckWaitHandler AckHandler { get; }

    public MessageRouter(ConcurrentDictionary<StringIdentifier, MessageConnectionHandler> handlers, AckWaitHandler ackHandler, string dbPath = "messaging_server.db") {
        this.handlers = handlers;
        this.dbPath = dbPath;

        // Delete db for testing
        using (var db = CreateDbContext()) {
            DbUtil.DeleteDb(db);
            db.Database.EnsureCreated();
            db.Messages
                .Where(e => e.State == MessageState.Pending || e.State == MessageState.AutoPending)
                .ExecuteUpdate(e => e.SetProperty(x => x.State, MessageState.Unsent));
        }
        AckHandler = ackHandler;
    }

    private ServerDbContext CreateDbContext() => new(dbPath);

    // Update the ack counter for the conversation for the person that sent the message
    public async Task<bool> UpdateHighestAckAsync(MessageData message) {
        var conversationKey = DbUtil.GetConversationKey(message.SourceId, message.TargetId);
        using var db = CreateDbContext();

        // Get the existing entry or null
        var existing = await db.HighestAcks
            .FirstOrDefaultAsync(e => e.ConversationKey == DbUtil.GetConversationKey(message.SourceId, message.TargetId) && e.SenderUsername == message.SourceId.Value);

        // Create new entry if one does not exist yet
        if (existing is null) {
            db.HighestAcks.Add(new() {
                ConversationKey = conversationKey,
                SenderUsername = message.SourceId.Value,
                HighestAck = message.Id
            });
        }
        else {
            // Check for duplicate messages
            if (existing.HighestAck < message.Id)
                existing.HighestAck = message.Id;
            else return false;
        }

        Console.WriteLine("Updating hightest ack");
        await db.SaveChangesAsync();
        return true;
    }

    // Used during unsent message sweep. Sends the wrapped message to the target and if successful removes from db
    private async Task SendWrapperAsync(MessageWrapper wrapper, ServerDbContext db) {
        // Get the MessageData object from the wrapper
        MessageData? message = MessagePackSerializer.Deserialize<MessageData>(wrapper.SerializedMessageData);

        if (message is null) {
            Console.WriteLine("Message data was null when attempting to send during sweep");
            return;
        }

        // If the target is connected attempt sending
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

    // Routes message to the target and places it in the db
    public async Task RouteMessageAsync(MessageData message) {
        string conversationKey = DbUtil.GetConversationKey(message.SourceId, message.TargetId);

        using var db = CreateDbContext();

        // Construct wrapper
        MessageWrapper wrapper = new() {
            ConversationKey = conversationKey,
            SequenceId = message.Id,
            SenderUsername = message.SourceId.Value,
            ReceiverUsername = message.TargetId.Value,
            SerializedMessageData = MessagePackSerializer.Serialize(message),
            StoredAtUtc = DateTime.UtcNow,
            State = MessageState.Pending
        };

        // Place unsent message in the db
        db.Messages.Add(wrapper);

        // Must save the changes before sending or the message might be lost
        try {
            await db.SaveChangesAsync();
        }

        catch (DbUpdateException) {
            Console.WriteLine("Server received duplicate message, discarded");
            return;
        }

        // Attempt sending
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

    // Attempts delivering all messages with unsent state to the specific user
    public async Task DeliverPendingMessagesAsync(StringIdentifier userId) {
        using var db = CreateDbContext();

        // Take control of unsent messages before attempting send, otherwise this could conflict with the automatic sweep
        await db.Messages
            .Where(m => m.ReceiverUsername == userId.Value && m.State == MessageState.Unsent)
            .ExecuteUpdateAsync(m => m.SetProperty(m => m.State, MessageState.Pending));

        // Get claimed messages
        var pendingMessages = await db.Messages
            .Where(m => m.ReceiverUsername == userId.Value && m.State == MessageState.Pending)
            .OrderBy(m => m.SequenceId)
            .ToListAsync();

        Console.WriteLine("Messages claimed");

        List<Task<bool>> sendTasks = [ ];

        var realPendingMessages = new List<MessageWrapper>();

        foreach (var wrapper in pendingMessages) {
            try {
                MessageData? messageData = MessagePackSerializer.Deserialize<MessageData>(wrapper.SerializedMessageData);

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

        // Await sending to finish
        bool[] results = await Task.WhenAll(sendTasks);
        int success = 0, failure = 0;

        for (int i = 0; i < sendTasks.Count; ++i) {
            if (results[i]) {
                ++success;

                // Remove message if the send was successful
                db.Messages.Remove(realPendingMessages[i]);
            }
            else {

                // Change state back to unsent for failed messages
                realPendingMessages[i].State = MessageState.Unsent;
                ++failure;
            }
        }

        if (realPendingMessages.Count > 0) {
            await db.SaveChangesAsync();

            // Debug log
            Console.WriteLine($"Delivered {success} pending messages to {userId.Value}, failed {failure}");
        }
    }

    // Starts a periodical sweep for unsent messages. This is a fallback for if the standard ways of message delivery fail
    public async Task StartUnsentSweepAsync(CancellationToken ct) {
        while (!ct.IsCancellationRequested) {
            Console.WriteLine("Sweeping...");
            using var db = CreateDbContext();

            var onlineUsers = handlers.Keys.Select(x => x.Value).ToArray();
            try {

                // Claim messages that can be sent to online users so they don't conflict with a reconnecting user
                await db.Messages
                    .Where(m => m.State == MessageState.Unsent && onlineUsers.Contains(m.ReceiverUsername))
                    .ExecuteUpdateAsync(m => m.SetProperty(x => x.State, MessageState.AutoPending), ct);
                
                // Extract db entry
                MessageWrapper[] unsent = await db.Messages
                    .Where(m => m.State == MessageState.AutoPending)
                    .OrderBy(m => m.SequenceId)
                    .ToArrayAsync(ct);

                // Try send
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
