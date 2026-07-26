using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Messaging.Server.Data;



public class AccountDbHandler {
    private readonly string dbPath;

    public AccountDbHandler(string dbPath = "messaging_server.db") {
        this.dbPath = dbPath;
    }
    private AccountDbContext CreateDbContext() => new(dbPath);

    public async Task<bool> ValidatePasswordAsync(string username, string password) { 
        using var db = CreateDbContext();

        Account account = await db.Accounts
            .FirstAsync(a => a.Username == username);

        PasswordHasher<Account> hasher = new();

        bool ret;

        var result = hasher.VerifyHashedPassword(null!, account.PasswordHash, password);

        if (result == PasswordVerificationResult.Failed)
            ret = false;
        else {
            ret = true;
            if (result == PasswordVerificationResult.SuccessRehashNeeded) {
                account.PasswordHash = hasher.HashPassword(null!, password);
                await db.SaveChangesAsync();
            }
        }

        return ret;
    }

    public async Task<bool> RegisterUserAsync(string username, string password) {
        PasswordHasher<Account> hasher = new();
        Account newAccount = new() {
            Username = username,
            PasswordHash = hasher.HashPassword(null!, password)
        }; 

        using var db = CreateDbContext();
        db.Accounts.Add(newAccount);

        try {
            await db.SaveChangesAsync();
        }
        catch (DbUpdateException) {
            return false;
        }
        return true;
    }

}