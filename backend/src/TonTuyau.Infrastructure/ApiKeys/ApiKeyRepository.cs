using Microsoft.EntityFrameworkCore;
using TonTuyau.Application.ApiKeys;
using TonTuyau.Infrastructure.Accounts;

namespace TonTuyau.Infrastructure.ApiKeys;

/// <summary>The API keys' database calls (EF Core, PostgreSQL). The rules are in <see cref="ApiKeyService"/>.</summary>
internal sealed class ApiKeyRepository(TonTuyauDbContext db) : IApiKeyRepository
{
    public async Task<IReadOnlyList<ApiKey>> ListAsync(CancellationToken cancellationToken)
    {
        List<ApiKeyRow> rows = await db.ApiKeys
            .AsNoTracking()
            .OrderByDescending(k => k.CreatedAt)
            .ThenByDescending(k => k.Id)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        return [.. rows.Select(r => (ApiKey)r)];
    }

    public async Task<ApiKey?> FindByHashAsync(string hash, CancellationToken cancellationToken)
    {
        ApiKeyRow? row = await db.ApiKeys
            .AsNoTracking()
            .SingleOrDefaultAsync(k => k.Hash == hash, cancellationToken)
            .ConfigureAwait(false);
        return row is null ? null : (ApiKey)row;
    }

    public Task<bool> NameTakenAsync(string name, CancellationToken cancellationToken)
    {
        string nameKey = KeyOf(name);
        return db.ApiKeys.AnyAsync(k => k.NameKey == nameKey, cancellationToken);
    }

    public async Task<ApiKey> AddAsync(
        string name,
        string prefix,
        string hash,
        ApiKeyKind kind,
        DateTimeOffset createdAt,
        string createdBy,
        CancellationToken cancellationToken)
    {
        ApiKeyRow row = new()
        {
            Name = name,
            NameKey = KeyOf(name),
            Prefix = prefix,
            Hash = hash,
            Kind = kind,
            Enabled = true,
            CreatedAt = createdAt,
            CreatedBy = createdBy,
        };
        db.ApiKeys.Add(row);
        await db
            .SaveChangesAsync(cancellationToken)
            .ConfigureAwait(false);
        return (ApiKey)row;
    }

    public async Task<ApiKey?> SetEnabledAsync(int id, bool enabled, CancellationToken cancellationToken)
    {
        ApiKeyRow? row = await db.ApiKeys
            .SingleOrDefaultAsync(k => k.Id == id, cancellationToken)
            .ConfigureAwait(false);
        if (row is null)
        {
            return null;
        }

        row.Enabled = enabled;
        await db
            .SaveChangesAsync(cancellationToken)
            .ConfigureAwait(false);
        return (ApiKey)row;
    }

    public async Task<string?> DeleteAsync(int id, CancellationToken cancellationToken)
    {
        ApiKeyRow? row = await db.ApiKeys
            .SingleOrDefaultAsync(k => k.Id == id, cancellationToken)
            .ConfigureAwait(false);
        if (row is null)
        {
            return null;
        }

        db.ApiKeys.Remove(row);
        await db
            .SaveChangesAsync(cancellationToken)
            .ConfigureAwait(false);
        return row.Name;
    }

    public Task TouchAsync(int id, DateTimeOffset at, DateTimeOffset notBefore, CancellationToken cancellationToken) =>
        db.ApiKeys
            .Where(k => k.Id == id && (k.LastUsedAt == null || k.LastUsedAt < notBefore))
            .ExecuteUpdateAsync(set => set.SetProperty(k => k.LastUsedAt, at), cancellationToken);

    /// <summary>"Firefox" and "firefox" are the same name.</summary>
    private static string KeyOf(string name) => name.ToLowerInvariant();
}
