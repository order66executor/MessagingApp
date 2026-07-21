using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Messaging.Server.Data;
using Messaging.Shared.Models;
using Messaging.Shared;
using Messaging.Shared.Data;

namespace Messaging.Server.Services;

public class MessageRouter  {
    private readonly ConcurrentDictionary<StringIdentifier, MessageConnectionHandler> handlers;
    private readonly string dbPath;

    public MessageRouter(ConcurrentDictionary<StringIdentifier, MessageConnectionHandler> handlers, string dbPath = "messaging_server.db") {
        this.handlers = handlers;
        this.dbPath = dbPath;
        using (var db = CreateDbContext()) {
            DbUtil.DeleteDb(db);
            db.Database.EnsureCreated();
        }
    }

    private ServerDbContext CreateDbContext() => new(dbPath);


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
            State = MessageState.Waiting
        };

        db.Messages.Add(wrapper);
        await db.SaveChangesAsync();

        if (handlers.TryGetValue(message.TargetId, out MessageConnectionHandler? targetHandler)) {
            try {
                await targetHandler.WriteToOutBufferAsync(message);
                wrapper.State = MessageState.Sent;
                await db.SaveChangesAsync();
                Console.WriteLine($"Message routed to {message.TargetId.Value}");
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

        var pendingMessages = await db.Messages
            .Where(m => m.ReceiverUsername == userId.Value && m.State == MessageState.Unsent)
            .OrderBy(m => m.SequenceId)
            .ToListAsync();

        foreach (var wrapper in pendingMessages) {
            try {
                MessageData? messageData = JsonSerializer.Deserialize<MessageData>(wrapper.SerializedMessageData);
                if (messageData != null) {
                    await handler.WriteToOutBufferAsync(messageData);
                    wrapper.State = MessageState.Sent;
                }
            }
            catch (Exception e) {
                Console.WriteLine($"Error delivering pending message to {userId.Value}: {e.Message}");
            }
        }

        if (pendingMessages.Count > 0) {
            await db.SaveChangesAsync();
            Console.WriteLine($"Delivered {pendingMessages.Count} pending messages to {userId.Value}");
        }
    }
}
