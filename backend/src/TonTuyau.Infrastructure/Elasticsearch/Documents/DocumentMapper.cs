using TonTuyau.Application.Library;
using TonTuyau.Application.Store;
using TonTuyau.Application.YouTube;

namespace TonTuyau.Infrastructure.Elasticsearch.Documents;

internal static class DocumentMapper
{
    /// <summary>The whole document of a video: its content (and that content's hash), then its bookkeeping.</summary>
    public static VideoDocument ToDocument(StoredVideo stored)
    {
        ArgumentNullException.ThrowIfNull(stored);
        return ToDocument(stored.Video) with
        {
            MediaMissing = stored.MediaMissing,
            ThumbnailArt = stored.ThumbnailArt is null ? null : ToDocument(stored.ThumbnailArt),
        };
    }

    /// <summary>The whole document of a channel: its content (and that content's hash), then its bookkeeping.</summary>
    public static ChannelDocument ToDocument(StoredChannel stored)
    {
        ArgumentNullException.ThrowIfNull(stored);
        return ToDocument(stored.Channel) with
        {
            ThumbArt = stored.ThumbArt is null ? null : ToDocument(stored.ThumbArt),
            BannerArt = stored.BannerArt is null ? null : ToDocument(stored.BannerArt),
        };
    }

    public static VideoDocument ToDocument(Video v)
    {
        VideoDocument doc = new(
            v.Id,
            v.ChannelId,
            v.ChannelName,
            v.Title,
            v.Description,
            v.Tags,
            v.Categories,
            TypeName(v.Type),
            v.Published,
            v.Downloaded,
            v.Duration.TotalSeconds,
            v.MediaSize,
            v.MediaPath,
            v.MediaPathCorrected,
            v.ThumbnailPath,
            v.Stats.Views,
            v.Stats.Likes,
            v.Stats.Dislikes,
            v.Tracks
                .Select(t => new TrackDocument(
                    t.Kind == MediaTrackKind.Video ? "video" : "audio",
                    t.Codec,
                    t.Bitrate,
                    t.Width,
                    t.Height))
                .ToArray(),
            StateName(v.YouTube.State),
            v.YouTube.CheckedAt,
            v.YouTube.Reason,
            v.YouTube.RefreshedAt);
        return doc with { ContentHash = StoreJson.ContentHash(doc) };
    }

    public static Video ToVideo(VideoDocument d) => new(
        d.Id,
        d.ChannelId,
        d.ChannelName,
        d.Title,
        d.Description,
        ParseType(d.Type),
        d.Published,
        d.Downloaded,
        TimeSpan.FromSeconds(d.DurationSeconds),
        d.MediaSize,
        d.MediaPath,
        d.MediaPathCorrected,
        d.ThumbnailPath,
        d.Tags,
        d.Categories,
        new VideoStats(d.Views, d.Likes, d.Dislikes),
        d.Tracks
            .Select(t => new MediaTrack(
                t.Kind == "video" ? MediaTrackKind.Video : MediaTrackKind.Audio,
                t.Codec,
                t.Bitrate,
                t.Width,
                t.Height))
            .ToArray(),
        StatusOf(d.YoutubeState, d.YoutubeCheckedAt, d.YoutubeReason, d.YoutubeRefreshedAt));

    public static ChannelDocument ToDocument(Channel c)
    {
        ChannelDocument doc = new(
            c.Id,
            c.Name,
            c.Description,
            c.Subscribers,
            c.Tags,
            c.ThumbPath,
            c.BannerPath,
            c.TvArtPath,
            StateName(c.YouTube.State),
            c.YouTube.CheckedAt,
            c.YouTube.Reason,
            c.YouTube.RefreshedAt);
        return doc with { ContentHash = StoreJson.ContentHash(doc) };
    }

    public static Channel ToChannel(ChannelDocument d) => new(
        d.Id,
        d.Name,
        d.Description,
        d.Subscribers,
        d.Tags,
        d.ThumbPath,
        d.BannerPath,
        d.TvartPath,
        StatusOf(d.YoutubeState, d.YoutubeCheckedAt, d.YoutubeReason, d.YoutubeRefreshedAt));

    public static ArtDocument ToDocument(ArtVersions art) => new(art.OriginalWidth, art.Widths);

    /// <summary>The state as stored (<c>youtube_state</c>): <c>not_checked</c>, <c>on_youtube</c>, …</summary>
    public static string StateName(YouTubeState state) => state switch
    {
        YouTubeState.OnYouTube => "on_youtube",
        YouTubeState.RetryLater => "retry_later",
        YouTubeState.Gone => "gone",
        YouTubeState.Private => "private",
        YouTubeState.Unlisted => "unlisted",
        _ => "not_checked",
    };

    public static YouTubeState ParseState(string? name) => name switch
    {
        "on_youtube" => YouTubeState.OnYouTube,
        "retry_later" => YouTubeState.RetryLater,
        "gone" => YouTubeState.Gone,
        "private" => YouTubeState.Private,
        "unlisted" => YouTubeState.Unlisted,
        _ => YouTubeState.NotChecked,
    };

    private static YouTubeStatus StatusOf(
        string? state,
        DateTimeOffset? checkedAt,
        string? reason,
        DateTimeOffset? refreshedAt) =>
        new(ParseState(state), checkedAt, reason, refreshedAt);

    public static ArtVersions? ToArt(ArtDocument? document) =>
        document is null ? null : new ArtVersions(document.Width, document.Sizes ?? []);

    public static string TypeName(VideoType type) => type switch
    {
        VideoType.Video => "video",
        VideoType.Short => "short",
        VideoType.Stream => "stream",
        _ => throw new ArgumentOutOfRangeException(nameof(type)),
    };

    public static VideoType ParseType(string name) => name switch
    {
        "video" => VideoType.Video,
        "short" => VideoType.Short,
        "stream" => VideoType.Stream,
        _ => throw new ArgumentOutOfRangeException(nameof(name), name, "Unknown video type in the index."),
    };
}
