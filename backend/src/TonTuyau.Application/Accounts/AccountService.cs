using System.Globalization;
using System.Security.Cryptography;
using Microsoft.Extensions.Logging;
using TonTuyau.Application.Browse;
using TonTuyau.Application.Configuration;
using TonTuyau.Application.Validation;

namespace TonTuyau.Application.Accounts;

/// <summary>
/// The accounts' rules: the first account at the first start, the password check at login, and (2.3.0) the admin's
/// accounts page (local accounts made, their role and password changed, disabled, deleted), one's own password, and
/// a session checked again. The last enabled local admin stays (with the directory down or misconfigured, it still
/// logs in), and nobody disables nor deletes their own account. The database calls are
/// <see cref="IAccountRepository"/>'s.
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

    /// <summary>The shortest password an admin or an account sets (2.3.0; the first start's is the environment's).</summary>
    public const int MinPasswordLength = 8;

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
            .AddAsync(Local(settings.AdminUser, settings.AdminPassword, AccountRole.Admin), cancellationToken)
            .ConfigureAwait(false);
        LogAdminCreated(settings.AdminUser);
    }

    /// <summary>
    /// The account these credentials open (its last login set to now), or null for any refusal: a disabled account, or
    /// one of the directory (its password isn't TonTuyau's), too.
    /// </summary>
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
        if (account is null || !valid || account.Disabled || account.Source != AccountSource.Local)
        {
            return null;
        }

        DateTimeOffset now = clock.GetUtcNow();
        await accounts.SetLastLoginAsync(account.Id, now, cancellationToken).ConfigureAwait(false);
        return account with { LastLoginAt = now };
    }

    /// <summary>
    /// The account a session is of, when it may go on: still there, enabled, with the stamp it was made with (its
    /// password, role and state unchanged since); null otherwise: the session ends.
    /// </summary>
    public async Task<Account?> SessionAsync(int id, string? stamp, CancellationToken cancellationToken)
    {
        Account? account = await accounts.FindAsync(id, cancellationToken).ConfigureAwait(false);
        return account is null || account.Disabled || stamp is null || account.Stamp != stamp ? null : account;
    }

    public Task<Account?> FindAsync(int id, CancellationToken cancellationToken) =>
        accounts.FindAsync(id, cancellationToken);

    /// <returns>Every account, by name.</returns>
    public Task<IReadOnlyList<Account>> ListAsync(CancellationToken cancellationToken) =>
        accounts.ListAsync(cancellationToken);

    /// <summary>A local account, enabled, made by an admin (2.3.0).</summary>
    /// <exception cref="ValidationException">
    /// The name is missing, too long or holds control characters; the password is missing, too short or too long; the
    /// role is missing or unknown.
    /// </exception>
    /// <exception cref="ConflictException">An account already has this name (whatever the case).</exception>
    public async Task<Account> CreateAsync(
        string? username,
        string? password,
        string? role,
        string by,
        CancellationToken cancellationToken)
    {
        ValidationErrors errors = new();
        RequestRules.Text(errors, "username", username, required: true, MaxUsernameLength);
        if (username is not null && username.Any(char.IsControl))
        {
            errors.Add("username", "must not hold control characters (line breaks, tabs…).");
        }

        CheckPassword(errors, "password", password);
        if (role is null)
        {
            errors.Add("role", $"is required: one of {ApiValues.AccountRoles.Choices}.");
        }

        RequestRules.OneOf(errors, "role", role, ApiValues.AccountRoles);
        errors.ThrowIfAny(RequestRules.Subject);

        string name = username!.Trim();
        if (await accounts.NameTakenAsync(name, cancellationToken).ConfigureAwait(false))
        {
            throw new ConflictException($"An account is already called “{name}”.");
        }

        Account account = await accounts
            .AddAsync(Local(name, password!, ApiValues.AccountRoles[role!]), cancellationToken)
            .ConfigureAwait(false);
        LogCreated(account.Username, account.Role, by);
        return account;
    }

    /// <summary>
    /// Changes an account (2.3.0): a local one's role and password; any one disabled or enabled again. Its sessions end
    /// at their next check.
    /// </summary>
    /// <param name="byId">The admin changing it: their own account can't be disabled.</param>
    /// <exception cref="NotFoundException">There's no account with this id.</exception>
    /// <exception cref="ValidationException">The role is unknown; the password too short or too long.</exception>
    /// <exception cref="ConflictException">
    /// A directory account's role or password (they're the directory's); the last enabled local admin demoted or
    /// disabled; one's own account disabled.
    /// </exception>
    public async Task<Account> ChangeAsync(
        int id,
        string? role,
        string? password,
        bool? disabled,
        int byId,
        string by,
        CancellationToken cancellationToken)
    {
        ValidationErrors errors = new();
        RequestRules.OneOf(errors, "role", role, ApiValues.AccountRoles);
        if (password is not null)
        {
            CheckPassword(errors, "password", password);
        }

        errors.ThrowIfAny(RequestRules.Subject);
        Account account = await accounts.FindAsync(id, cancellationToken).ConfigureAwait(false) ?? throw NotFound(id);
        AccountRole newRole = role is null ? account.Role : ApiValues.AccountRoles[role];
        bool newDisabled = disabled ?? account.Disabled;
        if (account.Source == AccountSource.Ldap && (newRole != account.Role || password is not null))
        {
            throw new ConflictException(
                $"“{account.Username}” is the directory's: its role comes from its groups, its password is changed there.");
        }

        if (newDisabled && !account.Disabled && account.Id == byId)
        {
            throw new ConflictException("Your own account can't be disabled.");
        }

        bool stopsBeingAdmin = newRole != AccountRole.Admin || newDisabled;
        if (stopsBeingAdmin && await IsLastLocalAdminAsync(account, cancellationToken).ConfigureAwait(false))
        {
            throw LastLocalAdmin(account);
        }

        Account changed = account with
        {
            Role = newRole,
            Disabled = newDisabled,
            PasswordHash = password is null ? account.PasswordHash : hasher.Hash(password),
        };
        if (changed == account)
        {
            return account;
        }

        changed = changed with { Stamp = NewStamp() };
        await accounts.UpdateAsync(changed, cancellationToken).ConfigureAwait(false);
        LogChanged(changed.Username, changed.Role, changed.Disabled, password is not null, by);
        return changed;
    }

    /// <summary>Deletes an account for good (2.3.0); a directory one comes back at its next login.</summary>
    /// <exception cref="NotFoundException">There's no account with this id.</exception>
    /// <exception cref="ConflictException">One's own account, or the last enabled local admin.</exception>
    public async Task DeleteAsync(int id, int byId, string by, CancellationToken cancellationToken)
    {
        Account account = await accounts.FindAsync(id, cancellationToken).ConfigureAwait(false) ?? throw NotFound(id);
        if (account.Id == byId)
        {
            throw new ConflictException("Your own account can't be deleted.");
        }

        if (await IsLastLocalAdminAsync(account, cancellationToken).ConfigureAwait(false))
        {
            throw LastLocalAdmin(account);
        }

        await accounts.DeleteAsync(id, cancellationToken).ConfigureAwait(false);
        LogDeleted(account.Username, by);
    }

    /// <summary>
    /// A local account changes its own password (2.3.0, My account), the current one first. Its other sessions end at
    /// their next check; the caller keeps its own with the account given back (its new stamp).
    /// </summary>
    /// <exception cref="NotFoundException">The account is gone.</exception>
    /// <exception cref="ValidationException">The current password is wrong; the new one too short or too long.</exception>
    /// <exception cref="ConflictException">A directory account: its password is the directory's.</exception>
    public async Task<Account> ChangeOwnPasswordAsync(
        int id,
        string? current,
        string? password,
        CancellationToken cancellationToken)
    {
        Account account = await accounts.FindAsync(id, cancellationToken).ConfigureAwait(false) ?? throw NotFound(id);
        if (account.Source != AccountSource.Local)
        {
            throw new ConflictException("Your account is the directory's: its password is changed there.");
        }

        ValidationErrors errors = new();
        bool currentRight = current is not null
            && current.Length <= MaxPasswordLength
            && hasher.Verify(current, account.PasswordHash ?? _unknownUserHash.Value);
        if (!currentRight)
        {
            errors.Add("current", "is wrong.");
        }

        CheckPassword(errors, "password", password);
        errors.ThrowIfAny(RequestRules.Subject);
        Account changed = account with { PasswordHash = hasher.Hash(password!), Stamp = NewStamp() };
        await accounts.UpdateAsync(changed, cancellationToken).ConfigureAwait(false);
        LogOwnPassword(changed.Username);
        return changed;
    }

    private Account Local(string username, string password, AccountRole role) =>
        new(
            0,
            username,
            DisplayName: null,
            role,
            AccountSource.Local,
            Disabled: false,
            hasher.Hash(password),
            NewStamp(),
            clock.GetUtcNow(),
            LastLoginAt: null);

    private async Task<bool> IsLastLocalAdminAsync(Account account, CancellationToken cancellationToken)
    {
        if (account.Source != AccountSource.Local || account.Role != AccountRole.Admin || account.Disabled)
        {
            return false;
        }

        IReadOnlyList<Account> all = await accounts.ListAsync(cancellationToken).ConfigureAwait(false);
        return !all.Any(a =>
            a.Id != account.Id && a.Source == AccountSource.Local && a.Role == AccountRole.Admin && !a.Disabled);
    }

    private static void CheckPassword(ValidationErrors errors, string field, string? password)
    {
        if (string.IsNullOrEmpty(password))
        {
            errors.Add(field, "is required.");
        }
        else if (password.Length < MinPasswordLength || password.Length > MaxPasswordLength)
        {
            errors.Add(
                field,
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"must be {MinPasswordLength} to {MaxPasswordLength} characters."));
        }
    }

    /// <summary>A new random stamp: the sessions made with the one before end at their next check.</summary>
    private static string NewStamp() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(16));

    private static ConflictException LastLocalAdmin(Account account) =>
        new($"“{account.Username}” is the last enabled local admin: it stays, to log in when the directory can't.");

    private static NotFoundException NotFound(int id) =>
        new(string.Create(CultureInfo.InvariantCulture, $"There's no account with the id {id}."));

    [LoggerMessage(Level = LogLevel.Information, Message = "Created the admin account {Username} (first start).")]
    private partial void LogAdminCreated(string username);

    [LoggerMessage(Level = LogLevel.Information, Message = "Account {Username} ({Role}) created by {By}.")]
    private partial void LogCreated(string username, AccountRole role, string by);

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "Account {Username} changed by {By}: {Role}, disabled {Disabled}, new password {Password}.")]
    private partial void LogChanged(string username, AccountRole role, bool disabled, bool password, string by);

    [LoggerMessage(Level = LogLevel.Information, Message = "Account {Username} deleted by {By}.")]
    private partial void LogDeleted(string username, string by);

    [LoggerMessage(Level = LogLevel.Information, Message = "{Username} changed their password.")]
    private partial void LogOwnPassword(string username);
}
