namespace TonTuyau.Application.ApiKeys;

/// <summary>
/// A key a program uses instead of logging in (1.5): the browser extension today. It may do only what its
/// <see cref="Kind"/> allows, never what an admin's session does. Every admin sees and manages every key.
/// </summary>
/// <param name="Name">Unique, whatever the case: what the admin calls it ("Firefox, laptop").</param>
/// <param name="Prefix">The secret's first characters (<c>tt_k7Qm</c>), to tell keys apart: the rest is never kept.</param>
/// <param name="CreatedBy">The account that created it.</param>
/// <param name="LastUsedAt">The last request it made, to the minute; null if never used.</param>
public sealed record ApiKey(
    int Id,
    string Name,
    string Prefix,
    ApiKeyKind Kind,
    bool Enabled,
    DateTimeOffset CreatedAt,
    string CreatedBy,
    DateTimeOffset? LastUsedAt);
