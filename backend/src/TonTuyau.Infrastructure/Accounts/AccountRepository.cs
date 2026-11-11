using Microsoft.EntityFrameworkCore;
using TonTuyau.Application.Accounts;

namespace TonTuyau.Infrastructure.Accounts;

/// <summary>The accounts' database calls (EF Core, PostgreSQL). The rules are in <see cref="AccountService"/>.</summary>
internal sealed class AccountRepository(TonTuyauDbContext db) : IAccountRepository
{
    public Task<bool> AnyAsync(CancellationToken cancellationToken) => db.Accounts.AnyAsync(cancellationToken);

    public async Task<IReadOnlyList<Account>> ListAsync(CancellationToken cancellationToken)
    {
        List<AccountRow> rows = await db.Accounts
            .AsNoTracking()
            .OrderBy(a => a.UsernameKey)
            .ThenBy(a => a.Id)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        return [.. rows.Select(r => (Account)r)];
    }

    public async Task<Account?> FindAsync(int id, CancellationToken cancellationToken)
    {
        AccountRow? row = await db.Accounts
            .AsNoTracking()
            .SingleOrDefaultAsync(a => a.Id == id, cancellationToken)
            .ConfigureAwait(false);
        return row is null ? null : (Account)row;
    }

    public async Task<Account?> FindByUsernameAsync(string username, CancellationToken cancellationToken)
    {
        AccountRow? row = await db.Accounts
            .AsNoTracking()
            .SingleOrDefaultAsync(a => a.Username == username, cancellationToken)
            .ConfigureAwait(false);
        return row is null ? null : (Account)row;
    }

    public Task<bool> NameTakenAsync(string username, CancellationToken cancellationToken)
    {
        string key = AccountRow.KeyOf(username);
        return db.Accounts.AnyAsync(a => a.UsernameKey == key, cancellationToken);
    }

    public async Task<Account> AddAsync(Account account, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(account);
        AccountRow row = new()
        {
            Username = account.Username,
            UsernameKey = AccountRow.KeyOf(account.Username),
            DisplayName = account.DisplayName,
            Role = account.Role,
            Source = account.Source,
            Disabled = account.Disabled,
            PasswordHash = account.PasswordHash,
            Stamp = account.Stamp,
            CreatedAt = account.CreatedAt,
            LastLoginAt = account.LastLoginAt,
        };
        db.Accounts.Add(row);
        await db
            .SaveChangesAsync(cancellationToken)
            .ConfigureAwait(false);
        return (Account)row;
    }

    public Task UpdateAsync(Account account, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(account);
        return db.Accounts
            .Where(a => a.Id == account.Id)
            .ExecuteUpdateAsync(
                set => set
                    .SetProperty(a => a.Role, account.Role)
                    .SetProperty(a => a.PasswordHash, account.PasswordHash)
                    .SetProperty(a => a.Disabled, account.Disabled)
                    .SetProperty(a => a.DisplayName, account.DisplayName)
                    .SetProperty(a => a.Stamp, account.Stamp),
                cancellationToken);
    }

    public Task DeleteAsync(int id, CancellationToken cancellationToken) =>
        db.Accounts
            .Where(a => a.Id == id)
            .ExecuteDeleteAsync(cancellationToken);

    public Task SetLastLoginAsync(int id, DateTimeOffset at, CancellationToken cancellationToken) =>
        db.Accounts
            .Where(a => a.Id == id)
            .ExecuteUpdateAsync(set => set.SetProperty(a => a.LastLoginAt, at), cancellationToken);
}
