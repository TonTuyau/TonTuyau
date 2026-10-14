namespace TonTuyau.Application.ApiKeys;

/// <summary>The API keys in TonTuyau's database (PostgreSQL): reads and writes, no rules.</summary>
public interface IApiKeyRepository
{
    /// <returns>Every key, the latest created first.</returns>
    Task<IReadOnlyList<ApiKey>> ListAsync(CancellationToken cancellationToken);

    /// <returns>The key whose secret has this hash, enabled or not, or null.</returns>
    Task<ApiKey?> FindByHashAsync(string hash, CancellationToken cancellationToken);

    /// <returns>Whether a key already has this name, ignoring case.</returns>
    Task<bool> NameTakenAsync(string name, CancellationToken cancellationToken);

    Task<ApiKey> AddAsync(
        string name,
        string prefix,
        string hash,
        ApiKeyKind kind,
        DateTimeOffset createdAt,
        string createdBy,
        CancellationToken cancellationToken);

    /// <returns>The key as it is now, or null when there's no key with this id.</returns>
    Task<ApiKey?> SetEnabledAsync(int id, bool enabled, CancellationToken cancellationToken);

    /// <returns>The deleted key's name, or null when there's no key with this id.</returns>
    Task<string?> DeleteAsync(int id, CancellationToken cancellationToken);

    /// <summary>Sets its last use to <paramref name="at"/>, unless it's already after <paramref name="notBefore"/>.</summary>
    Task TouchAsync(int id, DateTimeOffset at, DateTimeOffset notBefore, CancellationToken cancellationToken);
}
