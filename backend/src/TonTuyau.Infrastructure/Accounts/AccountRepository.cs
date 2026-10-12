using Microsoft.EntityFrameworkCore;
using TonTuyau.Application.Accounts;

namespace TonTuyau.Infrastructure.Accounts;

/// <summary>The accounts' database calls (EF Core, PostgreSQL). The rules are in <see cref="AccountService"/>.</summary>
internal sealed class AccountRepository(TonTuyauDbContext db) : IAccountRepository
{
    public Task<bool> AnyAsync(CancellationToken cancellationToken) => db.Accounts.AnyAsync(cancellationToken);

    public async Task<Account?> FindByUsernameAsync(string username, CancellationToken cancellationToken)
    {
        AccountRow? row = await db.Accounts
            .AsNoTracking()
            .SingleOrDefaultAsync(a => a.Username == username, cancellationToken)
            .ConfigureAwait(false);
        return row is null ? null : (Account)row;
    }

    public async Task AddAsync(
        string username,
        string passwordHash,
        DateTimeOffset createdAt,
        CancellationToken cancellationToken)
    {
        db.Accounts.Add(new AccountRow
        {
            Username = username,
            PasswordHash = passwordHash,
            CreatedAt = createdAt,
        });
        await db
            .SaveChangesAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public Task SetLastLoginAsync(int id, DateTimeOffset at, CancellationToken cancellationToken) =>
        db.Accounts
            .Where(a => a.Id == id)
            .ExecuteUpdateAsync(set => set.SetProperty(a => a.LastLoginAt, at), cancellationToken);
}
