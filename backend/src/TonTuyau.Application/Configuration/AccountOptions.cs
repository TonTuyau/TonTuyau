namespace TonTuyau.Application.Configuration;

/// <param name="AdminUser">The name of the account created at the first start, when there's none yet.</param>
/// <param name="AdminPassword">Its password: needed only then, and never stored as such (an Argon2id hash).</param>
/// <param name="CookieSecure">
/// The login cookie only travels over HTTPS (the default). False for a TonTuyau browsed over plain HTTP, where browsers
/// would drop a secure cookie.
/// </param>
public sealed record AccountOptions(string AdminUser, string AdminPassword, bool CookieSecure);
