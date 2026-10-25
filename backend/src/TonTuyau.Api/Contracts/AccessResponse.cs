namespace TonTuyau.Api.Contracts;

/// <summary>
/// Whether the library is for logged-in accounts only (contract v2.13), open to anyone: the page shows the login page
/// then.
/// </summary>
public sealed record AccessResponse(bool AccountsOnly);
