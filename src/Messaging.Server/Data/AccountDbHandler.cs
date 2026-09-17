using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Messaging.Server.Data;


// Wraps the DbContext object for easier use
public class AccountDbHandler {
    private readonly string dbPath;

    public AccountDbHandler(string dbPath = "server_accounts.db") {
        this.dbPath = dbPath;
        using var db = CreateDbContext();

        // Reset every restart for testing purposes
        db.Database.EnsureDeleted();
        db.Database.EnsureCreated();
    }

    // Create a DbContext object
    private AccountDbContext CreateDbContext() => new(dbPath);

    // Validate a plaintext password against the database using the built-in password hasher
    public async Task<bool> ValidatePasswordAsync(string username, string password) { 
        using var db = CreateDbContext();

        // Get the account by the username
        Account account = await db.Accounts
            .FirstAsync(a => a.Username == username);

        PasswordHasher<Account> hasher = new();

        bool ret;

        // Check password
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

    // Registers account with username and password in the db
    public async Task<bool> RegisterUserAsync(string username, string password) {
        PasswordHasher<Account> hasher = new();
        Account newAccount = new() {
            Username = username,

            // Hash password before placing in db
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