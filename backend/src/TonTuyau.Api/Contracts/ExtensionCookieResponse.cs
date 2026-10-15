namespace TonTuyau.Api.Contracts;

/// <summary>What the extension's popup shows about the cookie TonTuyau has: null when there's none.</summary>
public sealed record ExtensionCookieResponse(YouTubeCookieResponse? Cookie);
