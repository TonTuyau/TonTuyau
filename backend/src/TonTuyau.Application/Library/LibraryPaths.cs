using System.Text.RegularExpressions;

namespace TonTuyau.Application.Library;

/// <summary>
/// The only path shapes TonTuyau ever turns into files. Anything else (traversal, other folders, other extensions)
/// is refused before it reaches the disk.
/// </summary>
public static partial class LibraryPaths
{
    /// <summary><c>/cache/videos/&lt;c&gt;/&lt;video_id&gt;.jpg</c> or <c>/cache/channels/&lt;channel_id&gt;_thumb.jpg</c> (banner, tvart).</summary>
    public static bool IsArtPath(string? path) => path is not null && ArtPath().IsMatch(path);

    /// <summary><c>&lt;channel_id&gt;/&lt;video_id&gt;.mp4</c> (relative to the media root).</summary>
    public static bool IsMediaPath(string? path) => path is not null && MediaPath().IsMatch(path);

    /// <summary><c>run-20261002T010000Z-1a2b3c</c>, as made by <c>SyncRun.NewId</c>.</summary>
    public static bool IsRunId(string? id) => id is not null && RunId().IsMatch(id);

    /// <summary>Whose image a (valid) art path is: <c>/cache/videos/a/&lt;id&gt;.jpg</c> → that video's thumbnail.</summary>
    /// <returns>Null when the path isn't an art path.</returns>
    public static ArtOwner? OwnerOf(string? path)
    {
        Match match = path is null ? Match.Empty : ArtPath().Match(path);
        if (!match.Success)
        {
            return null;
        }

        string what = match.Groups[1].Value;
        if (what.StartsWith("videos/", StringComparison.Ordinal))
        {
            return new ArtOwner(ArtSubject.VideoThumbnail, what[^11..]);
        }

        string channelId = what["channels/".Length..("channels/".Length + 24)];
        ArtSubject subject = match.Groups[2].Value switch
        {
            "thumb" => ArtSubject.ChannelThumb,
            "banner" => ArtSubject.ChannelBanner,
            _ => ArtSubject.ChannelTvArt,
        };
        return new ArtOwner(subject, channelId);
    }

    [GeneratedRegex(
        "^/cache/(videos/[A-Za-z0-9_-]/[A-Za-z0-9_-]{11}|channels/UC[A-Za-z0-9_-]{22}_(thumb|banner|tvart))\\.(jpg|jpeg|png|webp)\\z")]
    private static partial Regex ArtPath();

    [GeneratedRegex("^UC[A-Za-z0-9_-]{22}/[A-Za-z0-9_-]{11}\\.(mp4|webm|mkv|m4v|mov)\\z")]
    private static partial Regex MediaPath();

    [GeneratedRegex("^run-[0-9]{8}T[0-9]{6}Z-[0-9a-f]{6}\\z")]
    private static partial Regex RunId();
}
