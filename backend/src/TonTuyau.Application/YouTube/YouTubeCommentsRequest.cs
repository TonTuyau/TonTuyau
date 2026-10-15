namespace TonTuyau.Application.YouTube;

/// <summary>
/// Asking for a video's comments too (1.5): up to <paramref name="Max"/> (null: all of them), answers included, in
/// YouTube's own order ("Top comments").
/// </summary>
public sealed record YouTubeCommentsRequest(int? Max);
