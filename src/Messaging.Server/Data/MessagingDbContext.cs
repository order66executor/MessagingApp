using Microsoft.EntityFrameworkCore;
using Messaging.Server.Models;

namespace Messaging.Server.Data;

public class MessagingDbContext : DbContext {
    public DbSet<ServerMessageWrapper> Messages { get; set; }

    private readonly string dbPath;

    public MessagingDbContext(string dbPath = "messaging.db") {
        this.dbPath = dbPath;
    }

    protected override void OnConfiguring(DbContextOptionsBuilder options) {
        options.UseSqlite($"Data Source={dbPath}");
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder) {
        modelBuilder.Entity<ServerMessageWrapper>(entity => {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.ReceiverUsername, e.Delivered }); // fast lookup for offline delivery
            entity.HasIndex(e => new { e.ConversationKey, e.SequenceId }).IsUnique(); // enforce uniqueness
        });
    }
}
