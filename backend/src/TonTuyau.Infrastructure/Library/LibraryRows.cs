using TonTuyau.Application.Library;
using TonTuyau.Application.Store;
using TonTuyau.Application.YouTube;
using TonTuyau.Infrastructure.YouTube;

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
    public static VideoRow NewRow(Video video, LibraryOrigin origin, CategoryRow? category) => new()
    {
        Id = video.Id,
        Origin = origin,
        ChannelId = video.ChannelId,
        Title = video.Title,
        Description = video.Description,
        Type = video.Type,
        // PostgreSQL takes instants in UTC only (TubeArchivist's dates may carry an offset).
        Published = video.Published.ToUniversalTime(),
        Downloaded = video.Downloaded.ToUniversalTime(),
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
    public static ChannelRow NewRow(Channel channel, LibraryOrigin origin) => new()
    {
        Id = channel.Id,
        Origin = origin,
        Name = channel.Name,
        Description = channel.Description,
        Subscribers = channel.Subscribers,
        Tags = [.. channel.Tags],
        ThumbPath = channel.ThumbPath,
        BannerPath = channel.BannerPath,
        TvArtPath = channel.TvArtPath,
        YouTube = new ChannelStateRow { ChannelId = channel.Id },
    };

    /// <summary>The video as the row holds it, with its channel's name and its state on YouTube.</summary>
    public static Video ToVideo(VideoRow row) =>
        new(
            row.Id,
            row.ChannelId,
            row.Channel.Name,
            row.Title,
            row.Description,
            row.Type,
            row.Published,
            row.Downloaded,
            row.Duration,
            row.MediaSize,
            row.MediaPath,
            row.MediaPathCorrected,
            row.ThumbnailPath,
            row.Tags,
            row.Category is { } category ? [category.Name] : [],
            new VideoStats(row.Views, row.Likes, row.Dislikes),
            [.. row.Tracks.Select(t => new MediaTrack(t.Kind, t.Codec, t.Bitrate, t.Width, t.Height))],
            ToStatus(row.YouTube));

    /// <summary>The channel as the row holds it, with its state on YouTube.</summary>
    public static Channel ToChannel(ChannelRow row) =>
        new(
            row.Id,
            row.Name,
            row.Description,
            row.Subscribers,
            row.Tags,
            row.ThumbPath,
            row.BannerPath,
            row.TvArtPath,
            ToStatus(row.YouTube));

    /// <summary>What TonTuyau knows of the video on YouTube: its state, and once refreshed, what the row holds.</summary>
    public static VideoFacts FactsOf(VideoRow row) =>
        new(
            row.Id,
            row.ChannelId,
            ToStatus(row.YouTube),
            row.YouTube.RefreshedAt is null
                ? null
                : new RefreshedVideo(
                    row.Title,
                    row.Channel.Name,
                    row.Description,
                    row.Tags,
                    row.Category is { } category ? [category.Name] : [],
                    row.Views,
                    row.Likes,
                    row.CommentCount,
                    [.. row.Chapters.Select(c => new YouTubeChapter(c.Start, c.End, c.Title))],
                    row.ThumbnailPath));

    /// <summary>What TonTuyau knows of the channel on YouTube: its state, and once refreshed, what the row holds.</summary>
    public static ChannelFacts FactsOf(ChannelRow row) =>
        new(
            row.Id,
            ToStatus(row.YouTube),
            row.YouTube.RefreshedAt is null
                ? null
                : new RefreshedChannel(
                    row.Name,
                    row.Description,
                    row.Subscribers,
                    row.Tags,
                    row.ThumbPath,
                    row.BannerPath));

    /// <summary>
    /// Writes what a refresh learned: the state, and when it read the metadata, the same columns the import filled
    /// (the category is the caller's: it may be a new row). Without metadata (gone, to retry), the row keeps its own.
    /// </summary>
    public static void WriteFacts(VideoRow row, VideoFacts facts, CategoryRow? category)
    {
        WriteStatus(row.YouTube, facts.Status);
        if (facts.Refreshed is not { } refreshed)
        {
            return;
        }

        row.Title = refreshed.Title;
        row.Description = refreshed.Description;
        row.Tags = [.. refreshed.Tags];
        row.Category = category;
        row.Views = refreshed.Views;
        row.Likes = refreshed.Likes;
        row.CommentCount = refreshed.CommentCount;
        row.Chapters =
        [
            .. refreshed.Chapters.Select(c => new VideoChapterRow
            {
                Start = c.StartSeconds,
                End = c.EndSeconds,
                Title = c.Title,
            }),
        ];
        row.ThumbnailPath = refreshed.ThumbnailPath ?? row.ThumbnailPath;
    }

    /// <summary>Writes what a refresh learned of a channel, like <see cref="WriteFacts(VideoRow, VideoFacts, CategoryRow?)"/>.</summary>
    public static void WriteFacts(ChannelRow row, ChannelFacts facts)
    {
        WriteStatus(row.YouTube, facts.Status);
        if (facts.Refreshed is not { } refreshed)
        {
            return;
        }

        row.Name = refreshed.Name;
        row.Description = refreshed.Description;
        row.Subscribers = refreshed.Subscribers;
        row.Tags = [.. refreshed.Tags];
        row.ThumbPath = refreshed.ThumbPath ?? row.ThumbPath;
        row.BannerPath = refreshed.BannerPath ?? row.BannerPath;
    }

    public static StoredVideo ToStored(VideoRow row) =>
        new(ToVideo(row), row.MediaMissing, ToVersions(row.ThumbnailArt));

    public static StoredChannel ToStored(ChannelRow row) =>
        new(ToChannel(row), ToVersions(row.ThumbArt), ToVersions(row.BannerArt));

    public static ArtVersionsRow ToRow(ArtVersions versions) =>
        new() { OriginalWidth = versions.OriginalWidth, Widths = [.. versions.Widths] };

    private static void WriteStatus(VideoStateRow row, YouTubeStatus status)
    {
        row.State = status.State;
        row.CheckedAt = status.CheckedAt;
        row.Reason = status.Reason;
        row.RefreshedAt = status.RefreshedAt;
    }

    private static void WriteStatus(ChannelStateRow row, YouTubeStatus status)
    {
        row.State = status.State;
        row.CheckedAt = status.CheckedAt;
        row.Reason = status.Reason;
        row.RefreshedAt = status.RefreshedAt;
    }

    private static YouTubeStatus ToStatus(VideoStateRow row) =>
        new(row.State, row.CheckedAt, row.Reason, row.RefreshedAt);

    private static YouTubeStatus ToStatus(ChannelStateRow row) =>
        new(row.State, row.CheckedAt, row.Reason, row.RefreshedAt);

    private static ArtVersions? ToVersions(ArtVersionsRow? row) =>
        row is null ? null : new ArtVersions(row.OriginalWidth, row.Widths);
}
