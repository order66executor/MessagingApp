using Microsoft.EntityFrameworkCore;
using Messaging.Shared.Models;
using Messaging.Shared.Data;

namespace Messaging.Server.Data;

public class ServerDbContext : DbContextBase {
    public DbSet<MessageWrapper> Messages { get; set; }

    public ServerDbContext(string dbPath = "server_messaging.db") : base(dbPath) {

    }

    protected override void OnModelCreating(ModelBuilder modelBuilder) {
        modelBuilder.Entity<MessageWrapper>(entity => {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.ReceiverUsername, e.State }); // fast lookup for offline delivery
            entity.HasIndex(e => new { e.ConversationKey, e.SequenceId, e.SenderUsername }).IsUnique(); // enforce uniqueness
        });
    }
}
