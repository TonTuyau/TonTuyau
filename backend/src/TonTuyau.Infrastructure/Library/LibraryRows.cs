using TonTuyau.Application.Library;
using TonTuyau.Application.Store;
using TonTuyau.Application.YouTube;

namespace TonTuyau.Infrastructure.Library;

/// <summary>
/// The library's rows and the records the application uses: a new row from what the import brings, and what a read
/// gives (the row as it is: one set of columns, nothing laid over).
/// </summary>
internal static class LibraryRows
{
    /// <summary>
    /// A video's new row: its content, not checked on YouTube (TubeArchivist's own flag isn't kept), in its category
    /// (the store finds or adds it). TubeArchivist's playlists aren't kept.
    /// </summary>
    public static VideoRow NewRow(Video video, CategoryRow? category) => new()
    {
        Id = video.Id,
        Origin = LibraryOrigin.TubeArchivist,
        ChannelId = video.ChannelId,
        Title = video.Title,
        Description = video.Description,
        Type = video.Type,
        Published = video.Published,
        Downloaded = video.Downloaded,
        Duration = video.Duration,
        MediaSize = video.MediaSize,
        MediaPath = video.MediaPath,
        MediaPathCorrected = video.MediaPathCorrected,
        ThumbnailPath = video.ThumbnailPath,
        Tags = [.. video.Tags],
        Category = category,
        Views = video.Stats.Views,
        Likes = video.Stats.Likes,
        Dislikes = video.Stats.Dislikes,
        Tracks =
        [
            .. video.Tracks.Select(t => new MediaTrackRow
            {
                Kind = t.Kind,
                Codec = t.Codec,
                Bitrate = t.Bitrate,
                Width = t.Width,
                Height = t.Height,
            }),
        ],
        YouTube = new VideoStateRow { VideoId = video.Id },
    };

    /// <summary>A channel's new row: its content, not checked on YouTube. TubeArchivist's tabs aren't kept.</summary>
    public static ChannelRow NewRow(Channel channel) => new()
    {
        Id = channel.Id,
        Origin = LibraryOrigin.TubeArchivist,
        Name = channel.Name,
        Description = channel.Description,
        Subscribers = channel.Subscribers,
        Tags = [.. channel.Tags],
        ThumbPath = channel.ThumbPath,
        BannerPath = channel.BannerPath,
        TvArtPath = channel.TvArtPath,
        YouTube = new ChannelStateRow { ChannelId = channel.Id },
    };

    /// <summary>
    /// The video as the row holds it, with its channel's name. <see cref="Video.Active"/> is true: TubeArchivist's flag isn't kept, the state on
    /// YouTube says whether it's gone. <see cref="Video.LastRefreshed"/> is null: TubeArchivist's date isn't kept,
    /// TonTuyau's is the state's <see cref="YouTubeStatus.RefreshedAt"/>. No <see cref="Video.PlaylistIds"/>.
    /// </summary>
    public static Video ToVideo(VideoRow row) =>
        new(
            row.Id,
            row.ChannelId,
            row.Channel.Name,
            row.Title,
            row.Description,
            row.Type,
            true,
            row.Published,
            row.Downloaded,
            null,
            row.Duration,
            row.MediaSize,
            row.MediaPath,
            row.MediaPathCorrected,
            row.ThumbnailPath,
            row.Tags,
            row.Category is { } category ? [category.Name] : [],
            [],
            new VideoStats(row.Views, row.Likes, row.Dislikes),
            [.. row.Tracks.Select(t => new MediaTrack(t.Kind, t.Codec, t.Bitrate, t.Width, t.Height))],
            ToStatus(row.YouTube));

    /// <summary>
    /// The channel as the row holds it (<see cref="Channel.Active"/> true, <see cref="Channel.LastRefreshed"/> null,
    /// as for a video; no <see cref="Channel.Tabs"/>: the channel's page shows the types of videos it holds).
    /// </summary>
    public static Channel ToChannel(ChannelRow row) =>
        new(
            row.Id,
            row.Name,
            row.Description,
            row.Subscribers,
            true,
            null,
            row.Tags,
            [],
            row.ThumbPath,
            row.BannerPath,
            row.TvArtPath,
            ToStatus(row.YouTube));

    public static StoredVideo ToStored(VideoRow row) =>
        new(ToVideo(row), row.MediaMissing, ToVersions(row.ThumbnailArt));

    public static StoredChannel ToStored(ChannelRow row) =>
        new(ToChannel(row), ToVersions(row.ThumbArt), ToVersions(row.BannerArt));

    public static ArtVersionsRow ToRow(ArtVersions versions) =>
        new() { OriginalWidth = versions.OriginalWidth, Widths = [.. versions.Widths] };

    private static YouTubeStatus ToStatus(VideoStateRow row) =>
        new(row.State, row.CheckedAt, row.Reason, row.RefreshedAt);

    private static YouTubeStatus ToStatus(ChannelStateRow row) =>
        new(row.State, row.CheckedAt, row.Reason, row.RefreshedAt);

    private static ArtVersions? ToVersions(ArtVersionsRow? row) =>
        row is null ? null : new ArtVersions(row.OriginalWidth, row.Widths);
}
