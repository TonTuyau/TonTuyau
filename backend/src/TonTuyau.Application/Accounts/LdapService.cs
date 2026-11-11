using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using TonTuyau.Application.Browse;
using TonTuyau.Application.Validation;

namespace TonTuyau.Application.Accounts;

/// <summary>What the admin's LDAP page sets (2.3.0); <see cref="BindPassword"/> null keeps the one stored.</summary>
public sealed record LdapSettingsChange(
    bool Enabled,
    string? Url,
    bool StartTls,
    string? CaCertificate,
    string? BindDn,
    string? BindPassword,
    string? BaseDn,
    string? UidAttribute,
    string? UsernameAttribute,
    string? UsersFilter,
    string? AdminsFilter,
    string? PasswordAttribute,
    string? PasswordResetUrl);

/// <summary>A directory account, and whether each filter finds it: the admins filter makes it an admin.</summary>
public sealed record LdapMatch(LdapPerson Person, bool User, bool Admin)
{
    public AccountRole? Role => Admin ? AccountRole.Admin : User ? AccountRole.User : null;
}

/// <summary>An account a filter finds, and whether a local account has its name (then it can't log in).</summary>
public sealed record LdapFoundAccount(LdapPerson Person, bool Clash);

/// <summary>What one test found: the connection, who a filter finds, or one name looked up.</summary>
/// <param name="Error">Why it couldn't connect or log the service account in.</param>
public sealed record LdapTestResult(
    bool Connected,
    string? Error,
    int? FoundTotal,
    IReadOnlyList<LdapFoundAccount>? Found,
    LdapLookup? Lookup);

/// <summary>A name looked up: the account found (or null), each filter, the role it'd get, or why it's refused.</summary>
public sealed record LdapLookup(LdapPerson? Person, bool User, bool Admin, AccountRole? Role, string? Refused);

/// <summary>The tests the LDAP page runs, each on its own.</summary>
public enum LdapTestKind
{
    Connection,
    Admins,
    Users,
    Name,
}

/// <summary>
/// The LDAP directory's rules (2.3.0): its settings (checked when it's on, the service account's password kept when
/// none is typed), the tests, the local names it has too (they clash), and the role an account gets: an admin when the
/// admins filter finds it, a user when the users filter does, refused otherwise. The calls are
/// <see cref="ILdapDirectory"/>'s.
/// </summary>
public sealed partial class LdapService(
    ILdapSettingsRepository settings,
    ILdapDirectory directory,
    IAccountRepository accounts,
    TimeProvider clock,
    ILogger<LdapService> logger)
{
    /// <summary>How many accounts a filter's test lists.</summary>
    public const int FoundShown = 100;

    public Task<LdapSettings> GetAsync(CancellationToken cancellationToken) => settings.GetAsync(cancellationToken);

    /// <exception cref="ValidationException">On, with a field missing or wrong (each one named).</exception>
    public async Task<LdapSettings> SaveAsync(LdapSettingsChange change, string by, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(change);
        LdapSettings stored = await settings.GetAsync(cancellationToken).ConfigureAwait(false);
        LdapSettings saved = Merge(change, stored) with { ChangedBy = by, ChangedAt = clock.GetUtcNow() };
        if (saved.Enabled)
        {
            Check(saved);
        }

        await settings.SaveAsync(saved, cancellationToken).ConfigureAwait(false);
        LogSaved(saved.Enabled ? "on" : "off", by);
        return saved;
    }

    /// <summary>The local accounts whose name the directory has too (none while it's off or can't be reached).</summary>
    public async Task<IReadOnlyList<string>> ClashesAsync(LdapSettings current, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(current);
        if (!current.Enabled)
        {
            return [];
        }

        IReadOnlyList<Account> all = await accounts.ListAsync(cancellationToken).ConfigureAwait(false);
        List<string> clashes = [];
        try
        {
            foreach (Account local in all.Where(a => a.Source == AccountSource.Local))
            {
                LdapPerson? person = await directory
                    .FindUserAsync(current, local.Username, filter: null, cancellationToken)
                    .ConfigureAwait(false);
                if (person is not null)
                {
                    clashes.Add(local.Username);
                }
            }
        }
        catch (LdapUnavailableException ex)
        {
            LogUnavailable(ex.Message);
            return [];
        }

        return clashes;
    }

    /// <summary>A test of the settings as typed (not saved): the connection, a filter, or a name.</summary>
    /// <exception cref="ValidationException">A field missing or wrong (each one named); a name test without a name.</exception>
    public async Task<LdapTestResult> TestAsync(
        LdapSettingsChange typed,
        LdapTestKind kind,
        string? username,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(typed);
        LdapSettings stored = await settings.GetAsync(cancellationToken).ConfigureAwait(false);
        LdapSettings trying = Merge(typed, stored) with { Enabled = true };
        if (kind == LdapTestKind.Name && string.IsNullOrWhiteSpace(username))
        {
            throw new ValidationException(RequestRules.Subject, [new ValidationError("username", "is required.")]);
        }

        Check(trying);
        try
        {
            switch (kind)
            {
                case LdapTestKind.Connection:
                    await directory.CheckAsync(trying, cancellationToken).ConfigureAwait(false);
                    return new LdapTestResult(
                        Connected: true,
                        Error: null,
                        FoundTotal: null,
                        Found: null,
                        Lookup: null);
                case LdapTestKind.Admins:
                case LdapTestKind.Users:
                    string filter = kind == LdapTestKind.Admins ? trying.AdminsFilter : trying.UsersFilter;
                    LdapFound found = await directory
                        .FindAsync(trying, filter, FoundShown, cancellationToken)
                        .ConfigureAwait(false);
                    List<LdapFoundAccount> shown = [];
                    foreach (LdapPerson person in found.People)
                    {
                        bool clash = await LocalHasAsync(person.Username, cancellationToken).ConfigureAwait(false);
                        shown.Add(new LdapFoundAccount(person, clash));
                    }

                    return new LdapTestResult(Connected: true, Error: null, found.Total, shown, Lookup: null);
                default:
                    LdapLookup lookup = await LookUpAsync(trying, username!.Trim(), cancellationToken)
                        .ConfigureAwait(false);
                    return new LdapTestResult(Connected: true, Error: null, FoundTotal: null, Found: null, lookup);
            }
        }
        catch (LdapUnavailableException ex)
        {
            return new LdapTestResult(Connected: false, ex.Message, FoundTotal: null, Found: null, Lookup: null);
        }
    }

    /// <summary>The directory's account with this name, and whether each filter finds it; null: it has none.</summary>
    /// <exception cref="LdapUnavailableException">The directory can't be asked.</exception>
    public async Task<LdapMatch?> MatchAsync(LdapSettings current, string username, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(current);
        LdapPerson? admin = await directory
            .FindUserAsync(current, username, current.AdminsFilter, cancellationToken)
            .ConfigureAwait(false);
        LdapPerson? user = await directory
            .FindUserAsync(current, username, current.UsersFilter, cancellationToken)
            .ConfigureAwait(false);
        LdapPerson? person = admin ?? user ?? await directory
            .FindUserAsync(current, username, filter: null, cancellationToken)
            .ConfigureAwait(false);
        return person is null ? null : new LdapMatch(person, user is not null, admin is not null);
    }

    private async Task<LdapLookup> LookUpAsync(
        LdapSettings trying,
        string username,
        CancellationToken cancellationToken)
    {
        LdapMatch? match = await MatchAsync(trying, username, cancellationToken).ConfigureAwait(false);
        if (match is null)
        {
            return new LdapLookup(null, User: false, Admin: false, Role: null, "no such name under the base DN.");
        }

        if (await LocalHasAsync(match.Person.Username, cancellationToken).ConfigureAwait(false))
        {
            return new LdapLookup(match.Person, match.User, match.Admin, Role: null, "a local account has that name.");
        }

        return match.Role is null
            ? new LdapLookup(
                match.Person,
                match.User,
                match.Admin,
                Role: null,
                "neither the users filter nor the admins filter finds it.")
            : new LdapLookup(match.Person, match.User, match.Admin, match.Role, Refused: null);
    }

    private async Task<bool> LocalHasAsync(string username, CancellationToken cancellationToken)
    {
        Account? account = await accounts.FindByUsernameAsync(username, cancellationToken).ConfigureAwait(false);
        return account?.Source == AccountSource.Local
            || (account is null && await accounts.NameTakenAsync(username, cancellationToken).ConfigureAwait(false));
    }

    private static LdapSettings Merge(LdapSettingsChange change, LdapSettings stored) =>
        stored with
        {
            Enabled = change.Enabled,
            Url = Trim(change.Url),
            StartTls = change.StartTls,
            CaCertificate = string.IsNullOrWhiteSpace(change.CaCertificate) ? null : change.CaCertificate.Trim(),
            BindDn = Trim(change.BindDn),
            BindPassword = string.IsNullOrEmpty(change.BindPassword) ? stored.BindPassword : change.BindPassword,
            BaseDn = Trim(change.BaseDn),
            UidAttribute = Trim(change.UidAttribute),
            UsernameAttribute = Trim(change.UsernameAttribute),
            UsersFilter = Trim(change.UsersFilter),
            AdminsFilter = Trim(change.AdminsFilter),
            PasswordAttribute = Trim(change.PasswordAttribute),
            PasswordResetUrl = string.IsNullOrWhiteSpace(change.PasswordResetUrl)
                ? null
                : change.PasswordResetUrl.Trim(),
        };

    private static string Trim(string? text) => text?.Trim() ?? "";

    /// <exception cref="ValidationException">A field missing or wrong, each one named.</exception>
    private static void Check(LdapSettings s)
    {
        ValidationErrors errors = new();
        (string Field, string Value)[] required =
        [
            ("url", s.Url),
            ("bindDn", s.BindDn),
            ("baseDn", s.BaseDn),
            ("uidAttribute", s.UidAttribute),
            ("usernameAttribute", s.UsernameAttribute),
            ("usersFilter", s.UsersFilter),
            ("adminsFilter", s.AdminsFilter),
            ("passwordAttribute", s.PasswordAttribute),
        ];
        foreach ((string field, string value) in required.Where(r => r.Value.Length == 0))
        {
            errors.Add(field, "is required to use the directory.");
        }

        if (s.Url.Length > 0 && !LdapUrl().IsMatch(s.Url))
        {
            errors.Add("url", "is ldap://host:port or ldaps://host:port.");
        }

        if (string.IsNullOrEmpty(s.BindPassword))
        {
            errors.Add("bindPassword", "is required to use the directory (none is stored).");
        }

        (string Field, string Filter)[] filters = [("usersFilter", s.UsersFilter), ("adminsFilter", s.AdminsFilter)];
        foreach ((string field, string filter) in filters)
        {
            if (filter.Length > 0 && !(filter.StartsWith('(') && filter.EndsWith(')')))
            {
                errors.Add(field, "is written between parentheses, e.g. (memberof=…).");
            }
        }

        if (s.PasswordResetUrl is not null && !ResetUrl().IsMatch(s.PasswordResetUrl))
        {
            errors.Add("passwordResetUrl", "is an http:// or https:// address.");
        }

        if (s.CaCertificate is not null && !IsCertificate(s.CaCertificate))
        {
            errors.Add("caCertificate", "is a certificate in PEM (-----BEGIN CERTIFICATE-----…).");
        }

        errors.ThrowIfAny(RequestRules.Subject);
    }

    private static bool IsCertificate(string pem)
    {
        try
        {
            using X509Certificate2 certificate = X509Certificate2.CreateFromPem(pem);
            return true;
        }
        catch (CryptographicException)
        {
            return false;
        }
    }

    [GeneratedRegex(@"^ldaps?://[^\s/:]+(:\d{1,5})?$", RegexOptions.IgnoreCase)]
    private static partial Regex LdapUrl();

    [GeneratedRegex(@"^https?://\S+$", RegexOptions.IgnoreCase)]
    private static partial Regex ResetUrl();

    [LoggerMessage(Level = LogLevel.Information, Message = "LDAP directory settings saved ({State}) by {By}.")]
    private partial void LogSaved(string state, string by);

    [LoggerMessage(Level = LogLevel.Warning, Message = "The LDAP directory can't be asked: {Why}")]
    private partial void LogUnavailable(string why);
}
