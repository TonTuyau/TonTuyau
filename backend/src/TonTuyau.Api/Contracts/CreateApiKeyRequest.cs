namespace TonTuyau.Api.Contracts;

/// <param name="Name">1 to 60 characters, unique whatever the case ("Firefox, laptop").</param>
/// <param name="Kind">What the key may do: <c>browserExtension</c> (the only kind in 1.5).</param>
public sealed record CreateApiKeyRequest(string? Name, string? Kind);
