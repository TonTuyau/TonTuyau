namespace TonTuyau.Application.YouTube;

/// <summary>A video as a channel's tab lists it (2.0, B5: the YouTube syncs), newest first.</summary>
/// <param name="NotYet">A stream or premiere still to come, or live now: nothing to download yet.</param>
public sealed record YouTubeListed(string Id, bool NotYet);
