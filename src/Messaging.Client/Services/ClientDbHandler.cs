using System.Text.Json;

using Messaging.Client.Data;
using Messaging.Shared.Data;
using Messaging.Shared.Models;

using Microsoft.EntityFrameworkCore;

namespace Messaging.Client.Services;

public class ClientDbHandler {
    private readonly string dbPath;

    public event Action<MessageWrapper>? OnMessageAdded;

    public ClientDbHandler(string dbPath = "messaging_client.db") {
        this.dbPath = dbPath;
        using var db = CreateDbContext();
        // DbUtil.DeleteDb(db);
        db.Database.EnsureCreated();

        db.Messages
            .Where(m => m.State == MessageState.Pending)
            .ExecuteUpdate(m => m.SetProperty(x => x.State, MessageState.Unsent));
    }


    private ClientDbContext CreateDbContext() => new(dbPath);

    public async Task<MessageWrapper> PlaceMessageAsync(MessageData message, MessageState state) {
        using var db = CreateDbContext();

        MessageWrapper wrapper = new() {
            ConversationKey = DbUtil.GetConversationKey(message.SourceId, message.TargetId),
            SequenceId = message.Id,
            SenderUsername = message.SourceId.Value,
            ReceiverUsername = message.TargetId.Value,
            SerializedMessageData = JsonSerializer.SerializeToUtf8Bytes(message),
            StoredAtUtc = DateTime.UtcNow,
            State = state
        };


        try {
            db.Messages.Add(wrapper);
            await db.SaveChangesAsync();

            OnMessageAdded?.Invoke(wrapper);
        }
        catch (DbUpdateException) {
            Console.WriteLine("Message is a duplicate");
        }

        return wrapper;
    }


    public async Task UpdateMessageStateAsync(long id, MessageState state) {
        using var db = CreateDbContext();
        await db.Messages
            .Where(e => e.Id == id)
            .ExecuteUpdateAsync(e => e.SetProperty(x => x.State, state));

    }


    // gets highest id for target
    public async Task<long> GetHighestSequenceIdAsync(StringIdentifier target) {
        using var db = CreateDbContext();
        return await db.Messages
            .Where(m => m.ReceiverUsername == target.Value)
            .Select(m => (long?)m.SequenceId)
            .MaxAsync() ?? 0;
    }

    // returns all messages that are sent by or to user. ordered by SentAtUtc

    public async Task<MessageWrapper[]> GetMessagesAsync(string conversationKey) {
        using var db = CreateDbContext();
        return await db.Messages
            .AsNoTracking()
            .Where(m => m.ConversationKey == conversationKey)
            .OrderBy(m => m.StoredAtUtc)
            .ToArrayAsync();
    }

    public async Task<MessageWrapper[]> GetMessagesWithStateAsync(MessageState state) {
        using var db = CreateDbContext();

        return await db.Messages
            .Where(m => m.State == state)
            .OrderBy(m => m.SequenceId)
            .ToArrayAsync();

    }

    public async Task<string[]> GetConversationsAsync() {
        using var db = CreateDbContext();
        return await db.Messages
            .AsNoTracking()
            .Select(m => m.ConversationKey)
            .Distinct()
            .ToArrayAsync();
    }

}

