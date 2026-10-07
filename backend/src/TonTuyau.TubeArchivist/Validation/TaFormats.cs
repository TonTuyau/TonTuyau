using System.Globalization;
using System.Text.RegularExpressions;

namespace TonTuyau.TubeArchivist.Validation;

/// <summary>Formats of TubeArchivist ids, paths and dates. Paths are checked strictly: they end up on disk and in URLs.</summary>
internal static partial class TaFormats
{
    [GeneratedRegex("^[A-Za-z0-9_-]{11}\\z")]
    public static partial Regex VideoId();

    [GeneratedRegex("^UC[A-Za-z0-9_-]{22}\\z")]
    public static partial Regex ChannelId();

    /// <summary><c>/youtube/&lt;channel_id&gt;/&lt;video_id&gt;.&lt;ext&gt;</c></summary>
    [GeneratedRegex(
        "^/youtube/(?<channel>UC[A-Za-z0-9_-]{22})/(?<video>[A-Za-z0-9_-]{11})\\.(?<ext>mp4|webm|mkv|m4v|mov)\\z")]
    public static partial Regex MediaUrl();

    /// <summary><c>/cache/videos/&lt;c&gt;/&lt;video_id&gt;.jpg</c></summary>
    [GeneratedRegex("^/cache/videos/[A-Za-z0-9_-]/(?<video>[A-Za-z0-9_-]{11})\\.(jpg|jpeg|png|webp)\\z")]
    public static partial Regex VideoThumbUrl();

    /// <summary><c>/cache/channels/&lt;channel_id&gt;_(thumb|banner|tvart).jpg</c></summary>
    [GeneratedRegex(
        "^/cache/channels/(?<channel>UC[A-Za-z0-9_-]{22})_(?<kind>thumb|banner|tvart)\\.(jpg|jpeg|png|webp)\\z")]
    public static partial Regex ChannelArtUrl();

    public static bool IsCachePath(string path) => VideoThumbUrl().IsMatch(path) || ChannelArtUrl().IsMatch(path);

    public static bool TryParseDate(string? value, out DateTimeOffset date) =>
        DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out date);

    public static readonly IReadOnlyDictionary<string, Application.Library.VideoType> VideoTypes =
        new Dictionary<string, Application.Library.VideoType>(StringComparer.Ordinal)
        {
            ["videos"] = Application.Library.VideoType.Video,
            ["shorts"] = Application.Library.VideoType.Short,
            ["streams"] = Application.Library.VideoType.Stream,
        };
}
