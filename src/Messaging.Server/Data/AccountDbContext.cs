using Messaging.Shared.Data;

using Microsoft.EntityFrameworkCore;

namespace Messaging.Server.Data;

public class AccountDbContext : DbContextBase {

    public DbSet<Account> Accounts { get; set; }

    public AccountDbContext(string dbPath) : base(dbPath) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder) {
        modelBuilder.Entity<Account>(entity => {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Username).IsUnique();
        });
    }


}