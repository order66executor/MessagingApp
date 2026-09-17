using MessagePack;

using Messaging.Client.Data;
using Messaging.Shared.Data;
using Messaging.Shared.Models;

using Microsoft.EntityFrameworkCore;

namespace Messaging.Client.Services;

public class ClientDbHandler {

    // Path of db file
    private readonly string dbPath;


    // Event to notify UI 
    public event Action<MessageWrapper>? OnMessageAdded;

    public ClientDbHandler(string dbPath = "messaging_client.db") {
        this.dbPath = dbPath;
        using var db = CreateDbContext();
        // DbUtil.DeleteDb(db);

        // Create database with matching schema if not created already
        db.Database.EnsureCreated();

        // Set state of pending messages to unsent
        db.Messages
            .Where(m => m.State == MessageState.Pending)
            .ExecuteUpdate(m => m.SetProperty(x => x.State, MessageState.Unsent));
    }

    // Create a db context 
    private ClientDbContext CreateDbContext() => new(dbPath);

    // Wrap messages in a wrapper and place it in the db with state
    public async Task<MessageWrapper> PlaceMessageAsync(MessageData message, MessageState state) {
        using var db = CreateDbContext();

        MessageWrapper wrapper = new() {
            ConversationKey = DbUtil.GetConversationKey(message.SourceId, message.TargetId),
            SequenceId = message.Id,
            SenderUsername = message.SourceId.Value,
            ReceiverUsername = message.TargetId.Value,
            SerializedMessageData = MessagePackSerializer.Serialize(message),
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

    // Update the state of message with id to state
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

    // Returns all messages as an array with the given state
    public async Task<MessageWrapper[]> GetMessagesWithStateAsync(string username, MessageState state) {
        using var db = CreateDbContext();

        return await db.Messages
            .Where(m => m.State == state && m.SenderUsername == username)
            .OrderBy(m => m.SequenceId)
            .ToArrayAsync();

    }

    // Returns conversation keys that have messages stored in the db as an array
    public async Task<string[]> GetConversationsAsync() {
        using var db = CreateDbContext();
        return await db.Messages
            .AsNoTracking()
            .Select(m => m.ConversationKey)
            .Distinct()
            .ToArrayAsync();
    }

}

