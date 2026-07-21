using Microsoft.EntityFrameworkCore;

namespace Messaging.Shared.Data;

public class DbContextBase : DbContext {
    private readonly string dbPath;

    public DbContextBase(string dbPath = "messaging.db") {
        this.dbPath = dbPath;
    }

    protected sealed override void OnConfiguring(DbContextOptionsBuilder options) {
        options.UseSqlite($"Data Source={dbPath}");
    }
}