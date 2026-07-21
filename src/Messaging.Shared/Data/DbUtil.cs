using Messaging.Shared.Models;

using Microsoft.EntityFrameworkCore;

namespace Messaging.Shared.Data;

public class DbUtil {
    public static string GetConversationKey(StringIdentifier userA, StringIdentifier userB) {
        return string.Compare(userA.Value, userB.Value, StringComparison.Ordinal) < 0
            ? $"{userA.Value}::{userB.Value}"
            : $"{userB.Value}::{userA.Value}";
    }

    public static async Task<long> GetHighestSequenceIdAsync(DbSet<MessageWrapper> messages, string receiverUsername) {

        long maxSequenceId = await messages
            .Where(m => m.ReceiverUsername == receiverUsername)
            .Select(m => (long?)m.SequenceId)
            .MaxAsync() ?? 0;

        return maxSequenceId;

    }

    public static void DeleteDb(DbContext db) {
        db.Database.EnsureDeleted();
    }
}