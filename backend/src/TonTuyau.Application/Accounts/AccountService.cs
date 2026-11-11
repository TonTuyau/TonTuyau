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
/// logs in), and nobody disables nor deletes their own account. A name that isn't a local account's logs in from the
/// LDAP directory when it's on (its row made at its first login, its role the directory's filters', read again at
/// each login and session check); a name both local and the directory's is the local account's only. The database
/// calls are <see cref="IAccountRepository"/>'s, the directory's <see cref="ILdapDirectory"/>'s.
/// </summary>
public sealed partial class AccountService(
    IAccountRepository accounts,
    IPasswordHasher hasher,
    LdapService ldap,
    ILdapDirectory directory,
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
    /// The account these credentials open (its last login set to now), or null for any refusal: a disabled account
    /// too. A name that isn't a local account's is the directory's, when it's on.
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
        if (account?.Source == AccountSource.Local)
        {
            return !valid || account.Disabled
                ? null
                : await LoggedInAsync(account, cancellationToken).ConfigureAwait(false);
        }

        return wellFormed
            ? await LoginFromDirectoryAsync(username!, password!, cancellationToken).ConfigureAwait(false)
            : null;
    }

    /// <summary>
    /// A login from the directory (2.3.0): the account found, a filter finding it, its password right, its name no
    /// local account's; its row made or brought up to date (its name, its role: a new role, a new stamp).
    /// </summary>
    private async Task<Account?> LoginFromDirectoryAsync(
        string username,
        string password,
        CancellationToken cancellationToken)
    {
        LdapSettings settings = await ldap.GetAsync(cancellationToken).ConfigureAwait(false);
        if (!settings.Enabled)
        {
            return null;
        }

        try
        {
            LdapMatch? match = await ldap.MatchAsync(settings, username, cancellationToken).ConfigureAwait(false);
            if (match?.Role is not AccountRole role)
            {
                return null;
            }

            string name = match.Person.Username;
            Account? existing = await accounts.FindByUsernameAsync(name, cancellationToken).ConfigureAwait(false);
            bool clash = existing?.Source == AccountSource.Local
                || (existing is null && await accounts.NameTakenAsync(name, cancellationToken).ConfigureAwait(false));
            if (clash)
            {
                LogClash(name);
                return null;
            }

            bool right = await directory
                .CheckPasswordAsync(settings, match.Person.Dn, password, cancellationToken)
                .ConfigureAwait(false);
            if (!right || existing?.Disabled == true)
            {
                return null;
            }

            Account account = existing is null
                ? await accounts
                    .AddAsync(Directory(match.Person, role), cancellationToken)
                    .ConfigureAwait(false)
                : await UpToDateAsync(existing, match.Person, role, cancellationToken).ConfigureAwait(false);
            return await LoggedInAsync(account, cancellationToken).ConfigureAwait(false);
        }
        catch (LdapUnavailableException ex)
        {
            LogDirectoryUnavailable(ex.Message);
            return null;
        }
    }

    private async Task<Account> LoggedInAsync(Account account, CancellationToken cancellationToken)
    {
        DateTimeOffset now = clock.GetUtcNow();
        await accounts.SetLastLoginAsync(account.Id, now, cancellationToken).ConfigureAwait(false);
        return account with { LastLoginAt = now };
    }

    /// <summary>A directory account's row with its name and role as the directory says them now.</summary>
    private async Task<Account> UpToDateAsync(
        Account account,
        LdapPerson person,
        AccountRole role,
        CancellationToken cancellationToken)
    {
        Account current = account with
        {
            DisplayName = person.DisplayName,
            Role = role,
            Stamp = role == account.Role ? account.Stamp : NewStamp(),
        };
        if (current != account)
        {
            await accounts.UpdateAsync(current, cancellationToken).ConfigureAwait(false);
        }

        return current;
    }

    /// <summary>
    /// The account a session is of, when it may go on: still there, enabled, with the stamp it was made with (its
    /// password, role and state unchanged since); null otherwise: the session ends.
    /// </summary>
    public async Task<Account?> SessionAsync(int id, string? stamp, CancellationToken cancellationToken)
    {
        Account? account = await accounts.FindAsync(id, cancellationToken).ConfigureAwait(false);
        if (account is null || account.Disabled || stamp is null || account.Stamp != stamp)
        {
            return null;
        }

        return account.Source == AccountSource.Ldap
            ? await DirectorySessionAsync(account, cancellationToken).ConfigureAwait(false)
            : account;
    }

    /// <summary>
    /// A directory account's session (2.3.0): the directory asked again. Off, or not finding it in a filter any more:
    /// it ends; a new role: the row takes it, and the session ends (logged in again, it has it). The directory not
    /// answering keeps it.
    /// </summary>
    private async Task<Account?> DirectorySessionAsync(Account account, CancellationToken cancellationToken)
    {
        LdapSettings settings = await ldap.GetAsync(cancellationToken).ConfigureAwait(false);
        if (!settings.Enabled)
        {
            return null;
        }

        try
        {
            LdapMatch? match = await ldap
                .MatchAsync(settings, account.Username, cancellationToken)
                .ConfigureAwait(false);
            if (match?.Role is not AccountRole role)
            {
                return null;
            }

            Account current = await UpToDateAsync(account, match.Person, role, cancellationToken).ConfigureAwait(false);
            return current.Stamp == account.Stamp ? current : null;
        }
        catch (LdapUnavailableException ex)
        {
            LogDirectoryUnavailable(ex.Message);
            return account;
        }
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

        if (await DirectoryHasAsync(name, cancellationToken).ConfigureAwait(false))
        {
            throw new ConflictException($"The directory has “{name}”: a local account can't have its name too.");
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
    /// An account changes its own password (2.3.0, My account), the current one first: a local account's here, a
    /// directory one's in the directory (changed by its service account). Its other sessions end at their next check;
    /// the caller keeps its own with the account given back (its new stamp).
    /// </summary>
    /// <exception cref="NotFoundException">The account is gone.</exception>
    /// <exception cref="ValidationException">The current password is wrong; the new one too short or too long.</exception>
    /// <exception cref="ConflictException">
    /// A directory account while the directory is off or no longer has it, or the directory refusing the change.
    /// </exception>
    /// <exception cref="LdapUnavailableException">The directory can't be reached.</exception>
    public async Task<Account> ChangeOwnPasswordAsync(
        int id,
        string? current,
        string? password,
        CancellationToken cancellationToken)
    {
        Account account = await accounts.FindAsync(id, cancellationToken).ConfigureAwait(false) ?? throw NotFound(id);
        if (account.Source == AccountSource.Ldap)
        {
            return await ChangeDirectoryPasswordAsync(account, current, password, cancellationToken)
                .ConfigureAwait(false);
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

    private async Task<Account> ChangeDirectoryPasswordAsync(
        Account account,
        string? current,
        string? password,
        CancellationToken cancellationToken)
    {
        LdapSettings settings = await ldap.GetAsync(cancellationToken).ConfigureAwait(false);
        if (!settings.Enabled)
        {
            throw new ConflictException("The directory is off: your password can't be changed from TonTuyau now.");
        }

        ValidationErrors errors = new();
        CheckPassword(errors, "password", password);
        errors.ThrowIfAny(RequestRules.Subject);
        LdapPerson person = await directory
            .FindUserAsync(settings, account.Username, filter: null, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new ConflictException($"The directory no longer has “{account.Username}”.");
        bool currentRight = !string.IsNullOrEmpty(current)
            && current.Length <= MaxPasswordLength
            && await directory
                .CheckPasswordAsync(settings, person.Dn, current, cancellationToken)
                .ConfigureAwait(false);
        if (!currentRight)
        {
            throw new ValidationException(RequestRules.Subject, [new ValidationError("current", "is wrong.")]);
        }

        try
        {
            await directory
                .ChangePasswordAsync(settings, person.Dn, current!, password!, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (LdapRefusedException ex)
        {
            throw new ConflictException($"The directory refused the new password: {ex.Message}", ex);
        }

        Account changed = account with { Stamp = NewStamp() };
        await accounts.UpdateAsync(changed, cancellationToken).ConfigureAwait(false);
        LogOwnPassword(changed.Username);
        return changed;
    }

    /// <summary>Whether the directory (on) has this name; it can't be asked: no (logged).</summary>
    private async Task<bool> DirectoryHasAsync(string username, CancellationToken cancellationToken)
    {
        LdapSettings settings = await ldap.GetAsync(cancellationToken).ConfigureAwait(false);
        if (!settings.Enabled)
        {
            return false;
        }

        try
        {
            return await directory
                .FindUserAsync(settings, username, filter: null, cancellationToken)
                .ConfigureAwait(false) is not null;
        }
        catch (LdapUnavailableException ex)
        {
            LogDirectoryUnavailable(ex.Message);
            return false;
        }
    }

    private Account Directory(LdapPerson person, AccountRole role) =>
        new(
            0,
            person.Username,
            person.DisplayName,
            role,
            AccountSource.Ldap,
            Disabled: false,
            PasswordHash: null,
            NewStamp(),
            clock.GetUtcNow(),
            LastLoginAt: null);

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

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "Refused a directory login as {Username}: a local account has that name.")]
    private partial void LogClash(string username);

    [LoggerMessage(Level = LogLevel.Warning, Message = "The LDAP directory can't be asked: {Why}")]
    private partial void LogDirectoryUnavailable(string why);
}
