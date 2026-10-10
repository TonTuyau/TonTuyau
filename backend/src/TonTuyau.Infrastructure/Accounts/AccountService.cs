using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TonTuyau.Application.Configuration;
using TonTuyau.Application.Validation;

namespace TonTuyau.Infrastructure.Accounts;

/// <summary>The accounts: the database's schema and first account at startup, and the password check at login.</summary>
public sealed partial class AccountService(
    TonTuyauDbContext db,
    TonTuyauOptions options,
    TimeProvider clock,
    ILogger<AccountService> logger)
{
    /// <summary>
    /// What an unknown name is checked against, so that refusing it costs the same Argon2id run as a wrong password:
    /// the answer's timing doesn't tell which names exist.
    /// </summary>
    private static readonly string UnknownUserHash = PasswordHasher.Hash(
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)));

    /// <summary>
    /// Applies the migrations, then creates the admin account when there's no account at all (the first start).
    /// Without <c>TONTUYAU_ADMIN_PASSWORD</c> then, the start stops with an error saying so.
    /// </summary>
    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        await db.Database
            .MigrateAsync(cancellationToken)
            .ConfigureAwait(false);
        if (await db.Accounts
            .AnyAsync(cancellationToken)
            .ConfigureAwait(false))
        {
            return;
        }

        AccountOptions accounts = options.Accounts;
        if (string.IsNullOrWhiteSpace(accounts.AdminPassword))
        {
            throw new ValidationException(
                "configuration",
                [
                    new ValidationError(
                        ConfigKeys.EnvironmentName(ConfigKeys.AdminPassword),
                        "is required at the first start, to create the admin account (there's no account yet)."),
                ]);
        }

        db.Accounts.Add(new Account
        {
            Username = accounts.AdminUser,
            PasswordHash = PasswordHasher.Hash(accounts.AdminPassword),
            CreatedAt = clock.GetUtcNow(),
        });
        await db
            .SaveChangesAsync(cancellationToken)
            .ConfigureAwait(false);
        LogAdminCreated(accounts.AdminUser);
    }

    /// <summary>The account these credentials open (its last login set to now), or null for any refusal.</summary>
    public async Task<Account?> LoginAsync(string? username, string? password, CancellationToken cancellationToken)
    {
        Account? account = string.IsNullOrEmpty(username)
            ? null
            : await db.Accounts
                .SingleOrDefaultAsync(a => a.Username == username, cancellationToken)
                .ConfigureAwait(false);
        bool valid = PasswordHasher.Verify(password ?? "", account?.PasswordHash ?? UnknownUserHash);
        if (account is null || !valid)
        {
            return null;
        }

        account.LastLoginAt = clock.GetUtcNow();
        await db
            .SaveChangesAsync(cancellationToken)
            .ConfigureAwait(false);
        return account;
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Created the admin account {Username} (first start).")]
    private partial void LogAdminCreated(string username);
}
