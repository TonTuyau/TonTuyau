namespace TonTuyau.Application.YouTube;

/// <summary>The admin's YouTube page: the cookie (null when there's none) and the tools.</summary>
public sealed record YouTubeOverview(YouTubeCookieStatus? Cookie, YouTubeTools Tools);
