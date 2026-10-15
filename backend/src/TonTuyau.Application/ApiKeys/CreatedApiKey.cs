namespace TonTuyau.Application.ApiKeys;

/// <summary>A key just created, with its secret: the only time the secret is given (only its hash is kept).</summary>
public sealed record CreatedApiKey(ApiKey Key, string Secret);
