namespace TonTuyau.Application.Accounts;

/// <summary>The accounts in TonTuyau's database (PostgreSQL): reads and writes, no rules.</summary>
public interface IAccountRepository
{
    Task<bool> AnyAsync(CancellationToken cancellationToken);

    /// <returns>The account with exactly this name, or null.</returns>
    Task<Account?> FindByUsernameAsync(string username, CancellationToken cancellationToken);

    Task AddAsync(string username, string passwordHash, DateTimeOffset createdAt, CancellationToken cancellationToken);

    Task SetLastLoginAsync(int id, DateTimeOffset at, CancellationToken cancellationToken);
}
