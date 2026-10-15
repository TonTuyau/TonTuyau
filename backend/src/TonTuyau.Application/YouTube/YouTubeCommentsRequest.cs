namespace TonTuyau.Application.YouTube;

/// <summary>
/// Asking for a video's comments too (1.5): up to <paramref name="Max"/>, answers included, in YouTube's own order
/// ("Top comments").
/// </summary>
public sealed record YouTubeCommentsRequest(int Max);
