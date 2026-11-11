namespace TonTuyau.Application.Accounts;

/// <summary>An account of the directory: its DN, its name (the UID attribute's) and the name it shows.</summary>
public sealed record LdapPerson(string Dn, string Username, string? DisplayName);

/// <summary>Who a filter finds: how many, and the first ones.</summary>
public sealed record LdapFound(int Total, IReadOnlyList<LdapPerson> People);

/// <summary>The directory couldn't be reached, or its service account didn't log in: says why.</summary>
public sealed class LdapUnavailableException(string message, Exception? inner = null) : Exception(message, inner);

/// <summary>The directory refused what was asked (a password change): says why.</summary>
public sealed class LdapRefusedException(string message, Exception? inner = null) : Exception(message, inner);

/// <summary>
/// The LDAP directory (2.3.0), each call with the settings given (saved or, for a test, as typed), as its service
/// account unless said otherwise.
/// </summary>
public interface ILdapDirectory
{
    /// <summary>Connects, and logs the service account in.</summary>
    /// <exception cref="LdapUnavailableException">Not reached, or the service account refused.</exception>
    Task CheckAsync(LdapSettings settings, CancellationToken cancellationToken);

    /// <summary>Who a filter finds under the base DN, by name: how many, and the first <paramref name="max"/>.</summary>
    /// <exception cref="LdapUnavailableException">Not reached, the service account refused, or the filter was.</exception>
    Task<LdapFound> FindAsync(LdapSettings settings, string filter, int max, CancellationToken cancellationToken);

    /// <summary>The account with this name (the UID attribute), when <paramref name="filter"/> (null: any) finds it too.</summary>
    /// <exception cref="LdapUnavailableException">Not reached, or the service account refused.</exception>
    Task<LdapPerson?> FindUserAsync(
        LdapSettings settings,
        string username,
        string? filter,
        CancellationToken cancellationToken);

    /// <summary>Whether this password logs the account in (a bind as it).</summary>
    /// <exception cref="LdapUnavailableException">Not reached.</exception>
    Task<bool> CheckPasswordAsync(
        LdapSettings settings,
        string dn,
        string password,
        CancellationToken cancellationToken);

    /// <summary>
    /// Changes the account's password: as the service account (the Password Modify operation, or the password attribute
    /// written where there's none); the directory refusing that, as the account itself (its current password).
    /// </summary>
    /// <exception cref="LdapUnavailableException">Not reached.</exception>
    /// <exception cref="LdapRefusedException">The directory refused it, both ways.</exception>
    Task ChangePasswordAsync(
        LdapSettings settings,
        string dn,
        string current,
        string password,
        CancellationToken cancellationToken);
}
