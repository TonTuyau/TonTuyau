namespace TonTuyau.Application.YouTube;

/// <summary>A picture YouTube offers (a thumbnail, a channel's avatar or banner); sizes when YouTube gives them.</summary>
/// <param name="Id">yt-dlp's name for it, e.g. <c>avatar_uncropped</c>, <c>banner_uncropped</c>; null when none.</param>
public sealed record YouTubeThumbnail(string Url, int? Width, int? Height, string? Id);
