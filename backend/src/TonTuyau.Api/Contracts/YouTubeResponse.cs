using TonTuyau.Application.YouTube;

namespace TonTuyau.Api.Contracts;

/// <summary>The admin's YouTube page: the cookie (null when there's none) and the tools' versions (null: missing).</summary>
public sealed record YouTubeResponse(YouTubeCookieResponse? Cookie, string? Ytdlp, string? Deno)
{
    public static explicit operator YouTubeResponse(YouTubeOverview overview)
    {
        ArgumentNullException.ThrowIfNull(overview);
        return new YouTubeResponse(
            overview.Cookie is null ? null : (YouTubeCookieResponse)overview.Cookie,
            overview.Tools.YtdlpVersion,
            overview.Tools.DenoVersion);
    }
}
