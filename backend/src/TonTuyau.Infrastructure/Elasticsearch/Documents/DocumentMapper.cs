using TonTuyau.Application.Library;

namespace TonTuyau.Infrastructure.Elasticsearch.Documents;

internal static class DocumentMapper
{
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
            v.PlaylistIds,
            TypeName(v.Type),
            v.Active,
            v.Published,
            v.Downloaded,
            v.LastRefreshed,
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
                .ToArray());
        return doc with { ContentHash = StoreJson.ContentHash(doc) };
    }

    public static Video ToVideo(VideoDocument d) => new(
        d.Id,
        d.ChannelId,
        d.ChannelName,
        d.Title,
        d.Description,
        ParseType(d.Type),
        d.Active,
        d.Published,
        d.Downloaded,
        d.LastRefreshed,
        TimeSpan.FromSeconds(d.DurationSeconds),
        d.MediaSize,
        d.MediaPath,
        d.MediaPathCorrected,
        d.ThumbnailPath,
        d.Tags,
        d.Categories,
        d.PlaylistIds,
        new VideoStats(d.Views, d.Likes, d.Dislikes),
        d.Tracks
            .Select(t => new MediaTrack(
                t.Kind == "video" ? MediaTrackKind.Video : MediaTrackKind.Audio,
                t.Codec,
                t.Bitrate,
                t.Width,
                t.Height))
            .ToArray());

    public static ChannelDocument ToDocument(Channel c)
    {
        ChannelDocument doc = new(
            c.Id,
            c.Name,
            c.Description,
            c.Subscribers,
            c.Active,
            c.LastRefreshed,
            c.Tags,
            c.Tabs
                .Select(TypeName)
                .ToArray(),
            c.ThumbPath,
            c.BannerPath,
            c.TvArtPath);
        return doc with { ContentHash = StoreJson.ContentHash(doc) };
    }

    public static Channel ToChannel(ChannelDocument d) => new(
        d.Id,
        d.Name,
        d.Description,
        d.Subscribers,
        d.Active,
        d.LastRefreshed,
        d.Tags,
        d.Tabs
            .Select(ParseType)
            .ToArray(),
        d.ThumbPath,
        d.BannerPath,
        d.TvartPath);

    public static ArtDocument ToDocument(ArtVersions art) => new(art.OriginalWidth, art.Widths);

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
