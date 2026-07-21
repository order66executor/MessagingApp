using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Messaging.Server.Data;
using Messaging.Server.Models;
using Messaging.Shared;

namespace Messaging.Server.Services;

public class MessageRouter {
    private readonly ConcurrentDictionary<StringIdentifier, MessageConnectionHandler> handlers;
    private readonly MessagingDbContext db;

    public MessageRouter(ConcurrentDictionary<StringIdentifier, MessageConnectionHandler> handlers, MessagingDbContext db) {
        this.handlers = handlers;
        this.db = db;
    }

    private string GetConversationKey(StringIdentifier userA, StringIdentifier userB) {
        return string.Compare(userA.Value, userB.Value, StringComparison.Ordinal) < 0
            ? $"{userA.Value}::{userB.Value}"
            : $"{userB.Value}::{userA.Value}";
    }

    public async Task RouteMessageAsync(MessageData message) {
        string conversationKey = GetConversationKey(message.SourceId, message.TargetId);

        // Run DB operations synchronously in this simple implementation, or await EF Core async methods
        // To avoid concurrency issues on SequenceId generation for the same conversation, a more robust 
        // approach would involve transactions or retry logic. For now we use basic locking or just hope 
        // the unique index catches races and we can retry, but for simplicity we will just do a standard query.
        
        long maxSequenceId = await db.Messages
            .Where(m => m.ConversationKey == conversationKey)
            .Select(m => (long?)m.SequenceId)
            .MaxAsync() ?? 0;

        long nextSequenceId = maxSequenceId + 1;

        ServerMessageWrapper wrapper = new() {
            ConversationKey = conversationKey,
            SequenceId = nextSequenceId,
            SenderUsername = message.SourceId.Value,
            ReceiverUsername = message.TargetId.Value,
            SerializedMessageData = JsonSerializer.SerializeToUtf8Bytes(message),
            StoredAtUtc = DateTimeOffset.UtcNow,
            Delivered = false
        };

        db.Messages.Add(wrapper);
        await db.SaveChangesAsync();

        if (handlers.TryGetValue(message.TargetId, out MessageConnectionHandler? targetHandler)) {
            try {
                await targetHandler.WriteToOutBufferAsync(message);
                wrapper.Delivered = true;
                await db.SaveChangesAsync();
                Console.WriteLine($"Message routed to {message.TargetId.Value}");
            }
            catch (Exception e) {
                Console.WriteLine($"Failed to write to out buffer for {message.TargetId.Value}: {e.Message}");
            }
        }
        else {
            Console.WriteLine($"User {message.TargetId.Value} offline, message stored in DB");
        }
    }

    public async Task DeliverPendingMessagesAsync(StringIdentifier userId, MessageConnectionHandler handler) {
        var pendingMessages = await db.Messages
            .Where(m => m.ReceiverUsername == userId.Value && m.Delivered == false)
            .OrderBy(m => m.SequenceId)
            .ToListAsync();

        foreach (var wrapper in pendingMessages) {
            try {
                MessageData? messageData = JsonSerializer.Deserialize<MessageData>(wrapper.SerializedMessageData);
                if (messageData != null) {
                    await handler.WriteToOutBufferAsync(messageData);
                    wrapper.Delivered = true;
                }
            }
            catch (Exception e) {
                Console.WriteLine($"Error delivering pending message to {userId.Value}: {e.Message}");
            }
        }

        if (pendingMessages.Any()) {
            await db.SaveChangesAsync();
            Console.WriteLine($"Delivered {pendingMessages.Count} pending messages to {userId.Value}");
        }
    }
}
