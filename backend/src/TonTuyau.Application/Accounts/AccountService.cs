using System.Security.Cryptography;
using Microsoft.Extensions.Logging;
using TonTuyau.Application.Configuration;
using TonTuyau.Application.Validation;

namespace TonTuyau.Application.Accounts;

/// <summary>
/// The accounts' rules: the first account at the first start, and the password check at login. The database calls
/// are <see cref="IAccountRepository"/>'s.
/// </summary>
public sealed partial class AccountService(
    IAccountRepository accounts,
    IPasswordHasher hasher,
    TonTuyauOptions options,
    TimeProvider clock,
    ILogger<AccountService> logger)
{
    /// <summary>The longest name taken (the login body is 4 KB at most anyway).</summary>
    public const int MaxUsernameLength = 128;

    /// <summary>The longest password taken: enough for any passphrase, and a bound on what Argon2id is given.</summary>
    public const int MaxPasswordLength = 1024;

    /// <summary>
    /// What a refused login is checked against when there's no stored hash (an unknown or invalid name), so that it
    /// costs the same Argon2id run as a wrong password: the answer's timing doesn't tell which names exist.
    /// </summary>
    private readonly Lazy<string> _unknownUserHash = new(() =>
        hasher.Hash(Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))));

    /// <summary>
    /// Creates the admin account when there's no account at all (the first start). Without
    /// <c>TONTUYAU_ADMIN_PASSWORD</c> then, the start stops with an error saying so.
    /// </summary>
    public async Task EnsureAdminAsync(CancellationToken cancellationToken)
    {
        if (await accounts.AnyAsync(cancellationToken).ConfigureAwait(false))
        {
            return;
        }

        AccountOptions settings = options.Accounts;
        if (string.IsNullOrWhiteSpace(settings.AdminPassword))
        {
            throw new ValidationException(
                "configuration",
                [
                    new ValidationError(
                        ConfigKeys.EnvironmentName(ConfigKeys.AdminPassword),
                        "is required at the first start, to create the admin account (there's no account yet)."),
                ]);
        }

        await accounts
            .AddAsync(
                settings.AdminUser,
                hasher.Hash(settings.AdminPassword),
                clock.GetUtcNow(),
                cancellationToken)
            .ConfigureAwait(false);
        LogAdminCreated(settings.AdminUser);
    }

    /// <summary>The account these credentials open (its last login set to now), or null for any refusal.</summary>
    public async Task<Account?> LoginAsync(string? username, string? password, CancellationToken cancellationToken)
    {
        bool wellFormed = !string.IsNullOrEmpty(username)
            && username.Length <= MaxUsernameLength
            && !string.IsNullOrEmpty(password)
            && password.Length <= MaxPasswordLength;
        Account? account = wellFormed
            ? await accounts.FindByUsernameAsync(username!, cancellationToken).ConfigureAwait(false)
            : null;
        // Always one check, even for a refusal decided already: the same cost whatever the reason.
        bool valid = hasher.Verify(password ?? "", account?.PasswordHash ?? _unknownUserHash.Value);
        if (account is null || !valid)
        {
            return null;
        }

        DateTimeOffset now = clock.GetUtcNow();
        await accounts.SetLastLoginAsync(account.Id, now, cancellationToken).ConfigureAwait(false);
        return account with { LastLoginAt = now };
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Created the admin account {Username} (first start).")]
    private partial void LogAdminCreated(string username);
}
