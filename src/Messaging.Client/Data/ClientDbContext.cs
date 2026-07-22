using Messaging.Shared.Data;
using Messaging.Shared.Models;

using Microsoft.EntityFrameworkCore;

namespace Messaging.Client.Data;

public class ClientDbContext : DbContextBase {

    public DbSet<MessageWrapper> Messages { get; set; }

    public ClientDbContext(string dbPath) : base(dbPath) {}

    protected override void OnModelCreating(ModelBuilder modelBuilder) {
        modelBuilder.Entity<MessageWrapper>(entity => {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new {e.ReceiverUsername, e.SequenceId});
            entity.HasIndex(e => new {e.ConversationKey, e.StoredAtUtc}); // for looking up conversations
        });
    }
}