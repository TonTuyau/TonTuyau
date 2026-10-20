namespace TonTuyau.Application.Extension;

/// <summary>A video or channel as the browser extension sees it (2.0, B7).</summary>
/// <param name="RunId">While <see cref="ExtensionItemState.Waiting"/>: the run that adds it; null otherwise.</param>
public sealed record ExtensionItem(ExtensionItemState State, string? RunId = null);
