namespace TonTuyau.Application.ApiKeys;

/// <summary>
/// What a key may do: each endpoint open to keys says which kinds it takes, and every other endpoint refuses keys.
/// Chosen when the key is made, never changed.
/// </summary>
public enum ApiKeyKind
{
    /// <summary>TonTuyau's browser extension: the connection test and the YouTube cookie (1.5).</summary>
    BrowserExtension,
}
