namespace TonTuyau.Application.Configuration;

/// <summary>TonTuyau's PostgreSQL, where the accounts live.</summary>
/// <param name="Host">A host name, or <c>host:port</c> (5432 by default).</param>
public sealed record DatabaseOptions(string Host, string Name, string Username, string Password);
