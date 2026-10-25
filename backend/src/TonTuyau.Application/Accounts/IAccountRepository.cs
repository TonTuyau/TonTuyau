namespace TonTuyau.Application.Accounts;

/// <summary>The accounts in TonTuyau's database (PostgreSQL): reads and writes, no rules.</summary>
public interface IAccountRepository
{
    Task<bool> AnyAsync(CancellationToken cancellationToken);

    /// <returns>Every account, by name.</returns>
    Task<IReadOnlyList<Account>> ListAsync(CancellationToken cancellationToken);

    Task<Account?> FindAsync(int id, CancellationToken cancellationToken);

    /// <returns>The account with exactly this name, or null.</returns>
    Task<Account?> FindByUsernameAsync(string username, CancellationToken cancellationToken);

    /// <summary>Whether an account has this name, whatever the case.</summary>
    Task<bool> NameTakenAsync(string username, CancellationToken cancellationToken);

    /// <returns>The account added, with its id (the one given is ignored).</returns>
    Task<Account> AddAsync(Account account, CancellationToken cancellationToken);

    /// <summary>Writes what can change: the role, the password, disabled or not, the display name, the stamp.</summary>
    Task UpdateAsync(Account account, CancellationToken cancellationToken);

    Task DeleteAsync(int id, CancellationToken cancellationToken);

    Task SetLastLoginAsync(int id, DateTimeOffset at, CancellationToken cancellationToken);
}
