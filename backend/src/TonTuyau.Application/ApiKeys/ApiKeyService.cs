using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;
using TonTuyau.Application.Browse;
using TonTuyau.Application.Validation;

namespace TonTuyau.Application.ApiKeys;

/// <summary>
/// The API keys' rules (1.5): a unique name, a secret given once and kept only as its hash, enabling and disabling,
/// and checking a secret a request brings. The database calls are <see cref="IApiKeyRepository"/>'s.
/// </summary>
public sealed partial class ApiKeyService(
    IApiKeyRepository keys,
    TimeProvider clock,
    ILogger<ApiKeyService> logger)
{
    public const int MaxNameLength = 60;

    /// <summary>What every secret starts with: a TonTuyau key is recognisable (in a config file, in a leak scan).</summary>
    public const string SecretStart = "tt_";

    /// <summary>The random part: 40 characters out of 62, about 238 bits; far beyond guessing.</summary>
    public const int SecretRandomLength = 40;

    /// <summary>How much of the secret is kept to tell keys apart: <c>tt_</c> and 4 characters.</summary>
    public const int PrefixLength = 7;

    /// <summary>The last use is written at most once a minute per key, not at every request.</summary>
    public static readonly TimeSpan LastUseResolution = TimeSpan.FromMinutes(1);

    private const string SecretAlphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";

    public Task<IReadOnlyList<ApiKey>> ListAsync(CancellationToken cancellationToken) =>
        keys.ListAsync(cancellationToken);

    /// <exception cref="ValidationException">
    /// The name is missing, too long, or holds control characters; the kind is missing or unknown.
    /// </exception>
    /// <exception cref="ConflictException">A key already has this name (whatever the case).</exception>
    public async Task<CreatedApiKey> CreateAsync(
        string? name,
        string? kind,
        string createdBy,
        CancellationToken cancellationToken)
    {
        (string trimmed, ApiKeyKind validKind) = Valid(name, kind);
        if (await keys.NameTakenAsync(trimmed, cancellationToken).ConfigureAwait(false))
        {
            throw new ConflictException($"A key is already called “{trimmed}”.");
        }

        string secret = SecretStart + RandomNumberGenerator.GetString(SecretAlphabet, SecretRandomLength);
        ApiKey key = await keys
            .AddAsync(
                trimmed,
                secret[..PrefixLength],
                HashOf(secret),
                validKind,
                clock.GetUtcNow(),
                createdBy,
                cancellationToken)
            .ConfigureAwait(false);
        LogCreated(key.Name, createdBy);
        return new CreatedApiKey(key, secret);
    }

    /// <exception cref="NotFoundException">There's no key with this id.</exception>
    public async Task<ApiKey> SetEnabledAsync(int id, bool enabled, string by, CancellationToken cancellationToken)
    {
        ApiKey key = await keys.SetEnabledAsync(id, enabled, cancellationToken).ConfigureAwait(false)
            ?? throw NotFound(id);
        if (enabled)
        {
            LogEnabled(key.Name, by);
        }
        else
        {
            LogDisabled(key.Name, by);
        }

        return key;
    }

    /// <exception cref="NotFoundException">There's no key with this id.</exception>
    public async Task DeleteAsync(int id, string by, CancellationToken cancellationToken)
    {
        string name = await keys.DeleteAsync(id, cancellationToken).ConfigureAwait(false)
            ?? throw NotFound(id);
        LogDeleted(name, by);
    }

    /// <summary>
    /// The enabled key this secret belongs to (its last use noted), or null: unknown, malformed, or disabled.
    /// </summary>
    public async Task<ApiKey?> AuthenticateAsync(string? secret, CancellationToken cancellationToken)
    {
        if (secret is null
            || secret.Length != SecretStart.Length + SecretRandomLength
            || !secret.StartsWith(SecretStart, StringComparison.Ordinal))
        {
            return null;
        }

        ApiKey? key = await keys.FindByHashAsync(HashOf(secret), cancellationToken).ConfigureAwait(false);
        if (key is null || !key.Enabled)
        {
            return null;
        }

        DateTimeOffset now = clock.GetUtcNow();
        await keys.TouchAsync(key.Id, now, now - LastUseResolution, cancellationToken).ConfigureAwait(false);
        return key;
    }

    /// <summary>
    /// SHA-256, hex: the secret is random and long, so a fast hash is enough (a slow one, like the passwords'
    /// Argon2id, protects guessable secrets); it also lets the database find the key by its hash.
    /// </summary>
    public static string HashOf(string secret) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(secret)));

    private static (string Name, ApiKeyKind Kind) Valid(string? name, string? kind)
    {
        ValidationErrors errors = new();
        RequestRules.Text(errors, "name", name, required: true, MaxNameLength);
        if (name is not null && name.Any(char.IsControl))
        {
            errors.Add("name", "must not hold control characters (line breaks, tabs…).");
        }

        if (kind is null)
        {
            errors.Add("kind", $"is required: one of {ApiValues.ApiKeyKinds.Choices}.");
        }

        RequestRules.OneOf(errors, "kind", kind, ApiValues.ApiKeyKinds);
        errors.ThrowIfAny(RequestRules.Subject);
        return (name!.Trim(), ApiValues.ApiKeyKinds[kind!]);
    }

    private static NotFoundException NotFound(int id) =>
        new(string.Create(CultureInfo.InvariantCulture, $"There's no API key with the id {id}."));

    [LoggerMessage(Level = LogLevel.Information, Message = "API key “{Name}” created by {By}.")]
    private partial void LogCreated(string name, string by);

    [LoggerMessage(Level = LogLevel.Information, Message = "API key “{Name}” enabled by {By}.")]
    private partial void LogEnabled(string name, string by);

    [LoggerMessage(Level = LogLevel.Information, Message = "API key “{Name}” disabled by {By}.")]
    private partial void LogDisabled(string name, string by);

    [LoggerMessage(Level = LogLevel.Information, Message = "API key “{Name}” deleted by {By}.")]
    private partial void LogDeleted(string name, string by);
}
