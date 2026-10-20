namespace TonTuyau.Api.Contracts;

/// <summary>What the browser extension's connection test shows: the TonTuyau it reached, and as whom.</summary>
/// <param name="Version">TonTuyau's version (<c>1.5.0</c>): the extension can tell when it's too old for it.</param>
/// <param name="Name">"&lt;key name&gt; (API key)", or the account's name with a session.</param>
/// <param name="CanAdd">
/// It takes "Add to TonTuyau" (contract v2.6: <c>/api/extension/videos/{id}</c> and <c>/channels/{id}</c>); absent from
/// a TonTuyau before it, which the extension reads as no.
/// </param>
public sealed record ConnectionResponse(string Version, string Name, bool CanAdd = true);
