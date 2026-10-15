namespace TonTuyau.Api.Contracts;

/// <param name="Cookies">
/// A Netscape cookie file (7 fields a line, by tabs), as text: its <c>youtube.com</c> cookies are kept, the rest left out.
/// </param>
public sealed record SendCookieRequest(string? Cookies);
