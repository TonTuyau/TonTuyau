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
    /// <summary>A video's new row: its content, not checked on YouTube (TubeArchivist's own flag isn't kept).</summary>
    public static VideoRow NewRow(Video video) => new()
    {
        Id = video.Id,
        Origin = LibraryOrigin.TubeArchivist,
        ChannelId = video.ChannelId,
        ChannelName = video.ChannelName,
        Title = video.Title,
        Description = video.Description,
        Type = video.Type,
        Published = video.Published,
        Downloaded = video.Downloaded,
        LastRefreshed = video.LastRefreshed,
        Duration = video.Duration,
        MediaSize = video.MediaSize,
        MediaPath = video.MediaPath,
        MediaPathCorrected = video.MediaPathCorrected,
        ThumbnailPath = video.ThumbnailPath,
        Tags = [.. video.Tags],
        Categories = [.. video.Categories],
        PlaylistIds = [.. video.PlaylistIds],
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
    };

    /// <summary>A channel's new row: its content, not checked on YouTube.</summary>
    public static ChannelRow NewRow(Channel channel) => new()
    {
        Id = channel.Id,
        Origin = LibraryOrigin.TubeArchivist,
        Name = channel.Name,
        Description = channel.Description,
        Subscribers = channel.Subscribers,
        LastRefreshed = channel.LastRefreshed,
        Tags = [.. channel.Tags],
        Tabs = [.. channel.Tabs],
        ThumbPath = channel.ThumbPath,
        BannerPath = channel.BannerPath,
        TvArtPath = channel.TvArtPath,
    };

    /// <summary>
    /// The video as the row holds it. <see cref="Video.Active"/> is true: TubeArchivist's flag isn't kept, the state on
    /// YouTube says whether it's gone.
    /// </summary>
    public static Video ToVideo(VideoRow row) =>
        new(
            row.Id,
            row.ChannelId,
            row.ChannelName,
            row.Title,
            row.Description,
            row.Type,
            true,
            row.Published,
            row.Downloaded,
            row.LastRefreshed,
            row.Duration,
            row.MediaSize,
            row.MediaPath,
            row.MediaPathCorrected,
            row.ThumbnailPath,
            row.Tags,
            row.Categories,
            row.PlaylistIds,
            new VideoStats(row.Views, row.Likes, row.Dislikes),
            [.. row.Tracks.Select(t => new MediaTrack(t.Kind, t.Codec, t.Bitrate, t.Width, t.Height))],
            new YouTubeStatus(row.YouTubeState, row.YouTubeCheckedAt, row.YouTubeReason, row.YouTubeRefreshedAt));

    /// <summary>The channel as the row holds it (<see cref="Channel.Active"/> true, as for a video).</summary>
    public static Channel ToChannel(ChannelRow row) =>
        new(
            row.Id,
            row.Name,
            row.Description,
            row.Subscribers,
            true,
            row.LastRefreshed,
            row.Tags,
            row.Tabs,
            row.ThumbPath,
            row.BannerPath,
            row.TvArtPath,
            new YouTubeStatus(row.YouTubeState, row.YouTubeCheckedAt, row.YouTubeReason, row.YouTubeRefreshedAt));

    public static StoredVideo ToStored(VideoRow row) =>
        new(ToVideo(row), row.MediaMissing, ToVersions(row.ThumbnailArt));

    public static StoredChannel ToStored(ChannelRow row) =>
        new(ToChannel(row), ToVersions(row.ThumbArt), ToVersions(row.BannerArt));

    public static ArtVersionsRow ToRow(ArtVersions versions) =>
        new() { OriginalWidth = versions.OriginalWidth, Widths = [.. versions.Widths] };

    private static ArtVersions? ToVersions(ArtVersionsRow? row) =>
        row is null ? null : new ArtVersions(row.OriginalWidth, row.Widths);
}
