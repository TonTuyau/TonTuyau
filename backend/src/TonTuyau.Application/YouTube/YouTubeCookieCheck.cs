namespace TonTuyau.Application.YouTube;

/// <summary>What YouTube said when the cookie was tried; <paramref name="Reason"/> unless it works.</summary>
public sealed record YouTubeCookieCheck(YouTubeCookieState State, string? Reason);
