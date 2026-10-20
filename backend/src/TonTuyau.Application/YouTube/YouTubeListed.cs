namespace TonTuyau.Application.YouTube;

/// <summary>A video as a channel's tab lists it (2.0, B5: the YouTube syncs), newest first.</summary>
/// <param name="NotYet">A stream or premiere still to come, or live now: nothing to download yet.</param>
/// <param name="Title">As the tab lists it; null when it doesn't (a failed download is shown with it).</param>
public sealed record YouTubeListed(string Id, bool NotYet, string? Title = null);
