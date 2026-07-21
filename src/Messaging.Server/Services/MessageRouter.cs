using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Messaging.Server.Data;
using Messaging.Server.Models;
using Messaging.Shared;

namespace Messaging.Server.Services;

public class MessageRouter {
    private readonly ConcurrentDictionary<StringIdentifier, MessageConnectionHandler> handlers;
    private readonly string dbPath;

    public MessageRouter(ConcurrentDictionary<StringIdentifier, MessageConnectionHandler> handlers, string dbPath = "messaging.db") {
        this.handlers = handlers;
        this.dbPath = dbPath;
    }

    private MessagingDbContext CreateDbContext() => new(dbPath);

    private static string GetConversationKey(StringIdentifier userA, StringIdentifier userB) {
        return string.Compare(userA.Value, userB.Value, StringComparison.Ordinal) < 0
            ? $"{userA.Value}::{userB.Value}"
            : $"{userB.Value}::{userA.Value}";
    }

    public async Task RouteMessageAsync(MessageData message) {
        string conversationKey = GetConversationKey(message.SourceId, message.TargetId);

        using var db = CreateDbContext();

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
        using var db = CreateDbContext();

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

        if (pendingMessages.Count > 0) {
            await db.SaveChangesAsync();
            Console.WriteLine($"Delivered {pendingMessages.Count} pending messages to {userId.Value}");
        }
    }
}
