using System.Text.Json;

using Messaging.Client.Data;
using Messaging.Shared;
using Messaging.Shared.Data;
using Messaging.Shared.Models;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace Messaging.Client.Services;

public class ClientDbHandler {
    private readonly string dbPath;

    public ClientDbHandler(string dbPath = "messaging_client.db") {
        this.dbPath = dbPath;
        using var db = CreateDbContext();
        DbUtil.DeleteDb(db);
        db.Database.EnsureCreated();
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


        db.Messages.Add(wrapper);
        await db.SaveChangesAsync();

        return wrapper;
    }

    public async Task SaveDbChangesAsync() {
        using var db = CreateDbContext();
        await db.SaveChangesAsync();
    }


    // gets highest id for target
    public async Task<long> GetHighestIdAsync(StringIdentifier target) {
        using var db = CreateDbContext();
        return await db.Messages
            .Where(m => m.ReceiverUsername == target.Value)
            .Select(m => (long?)m.Id)
            .MaxAsync() ?? 0;
    }

    // returns all messages that are sent by or to user. ordered by SentAtUtc

    public async Task<MessageWrapper[]> GetMessagesAsync(StringIdentifier user) {
        using var db = CreateDbContext();
        return await db.Messages
            .AsNoTracking()
            .Where(m => m.SenderUsername == user.Value || m.ReceiverUsername == user.Value)
            .OrderBy(m => m.StoredAtUtc)
            .ToArrayAsync();
    }

}

