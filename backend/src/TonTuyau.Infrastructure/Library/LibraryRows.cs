using System.Security.Cryptography;
using System.Text.Json;
using TonTuyau.Application.Library;
using TonTuyau.Application.Store;
using TonTuyau.Application.YouTube;
using TonTuyau.Infrastructure.YouTube;

namespace TonTuyau.Infrastructure.Library;

/// <summary>
/// The library's rows and the records the application uses: what a sync writes (TubeArchivist's columns, their hash),
/// and what a read gives (TonTuyau's YouTube facts laid over, <see cref="YouTubeOverlay"/>).
/// </summary>
internal static class LibraryRows
{
    /// <summary>
    /// Hash of what a sync writes (TubeArchivist's content): the same video twice hashes the same. TonTuyau's own
    /// knowledge (<see cref="Video.YouTube"/>) isn't part of it: a sync never writes it.
    /// </summary>
    public static string ContentHash(Video video) => Hash(video with { YouTube = null });

    public static string ContentHash(Channel channel) => Hash(channel with { YouTube = null });

    /// <summary>Writes a sync's columns (TubeArchivist's content) and their hash; the rest of the row stays.</summary>
    public static void WriteContent(VideoRow row, Video video, string hash)
    {
        row.ChannelId = video.ChannelId;
        row.ChannelName = video.ChannelName;
        row.Title = video.Title;
        row.Description = video.Description;
        row.Type = video.Type;
        row.Active = video.Active;
        row.Published = video.Published;
        row.Downloaded = video.Downloaded;
        row.LastRefreshed = video.LastRefreshed;
        row.Duration = video.Duration;
        row.MediaSize = video.MediaSize;
        row.MediaPath = video.MediaPath;
        row.MediaPathCorrected = video.MediaPathCorrected;
        row.ThumbnailPath = video.ThumbnailPath;
        row.Tags = [.. video.Tags];
        row.Categories = [.. video.Categories];
        row.PlaylistIds = [.. video.PlaylistIds];
        row.Views = video.Stats.Views;
        row.Likes = video.Stats.Likes;
        row.Dislikes = video.Stats.Dislikes;
        row.Tracks =
        [
            .. video.Tracks.Select(t => new MediaTrackRow
            {
                Kind = t.Kind,
                Codec = t.Codec,
                Bitrate = t.Bitrate,
                Width = t.Width,
                Height = t.Height,
            }),
        ];
        row.ContentHash = hash;
    }

    public static VideoRow NewRow(Video video, string hash)
    {
        VideoRow row = new()
        {
            Id = video.Id,
            ChannelId = video.ChannelId,
            ChannelName = video.ChannelName,
            Title = video.Title,
            Description = video.Description,
            MediaPath = video.MediaPath,
            ThumbnailPath = video.ThumbnailPath,
            ContentHash = hash,
        };
        WriteContent(row, video, hash);
        return row;
    }

    public static void WriteContent(ChannelRow row, Channel channel, string hash)
    {
        row.Name = channel.Name;
        row.Description = channel.Description;
        row.Subscribers = channel.Subscribers;
        row.Active = channel.Active;
        row.LastRefreshed = channel.LastRefreshed;
        row.Tags = [.. channel.Tags];
        row.Tabs = [.. channel.Tabs];
        row.ThumbPath = channel.ThumbPath;
        row.BannerPath = channel.BannerPath;
        row.TvArtPath = channel.TvArtPath;
        row.ContentHash = hash;
    }

    public static ChannelRow NewRow(Channel channel, string hash)
    {
        ChannelRow row = new()
        {
            Id = channel.Id,
            Name = channel.Name,
            Description = channel.Description,
            ContentHash = hash,
        };
        WriteContent(row, channel, hash);
        return row;
    }

    /// <summary>The video as TonTuyau stands by it: TubeArchivist's content, TonTuyau's facts laid over.</summary>
    public static Video ToVideo(VideoRow row)
    {
        Video content = new(
            row.Id,
            row.ChannelId,
            row.ChannelName,
            row.Title,
            row.Description,
            row.Type,
            row.Active,
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
            [.. row.Tracks.Select(t => new MediaTrack(t.Kind, t.Codec, t.Bitrate, t.Width, t.Height))]);
        return YouTubeOverlay.On(content, FactsOf(row));
    }

    public static Channel ToChannel(ChannelRow row)
    {
        Channel content = new(
            row.Id,
            row.Name,
            row.Description,
            row.Subscribers,
            row.Active,
            row.LastRefreshed,
            row.Tags,
            row.Tabs,
            row.ThumbPath,
            row.BannerPath,
            row.TvArtPath);
        return YouTubeOverlay.On(content, FactsOf(row));
    }

    /// <summary>What TonTuyau read on YouTube; null when it never checked the video.</summary>
    public static VideoFacts? FactsOf(VideoRow row)
    {
        if (row.YouTubeState is null)
        {
            return null;
        }

        YouTubeStatus status = new(
            row.YouTubeState.Value,
            row.YouTubeCheckedAt,
            row.YouTubeReason,
            row.YouTubeRefreshedAt);
        RefreshedVideo? refreshed = row.YouTubeRefreshedAt is null || row.YouTubeTitle is null
            ? null
            : new RefreshedVideo(
                row.YouTubeTitle,
                row.YouTubeChannelName ?? "",
                row.YouTubeDescription ?? "",
                row.YouTubeTags,
                row.YouTubeCategories,
                row.YouTubeViews ?? 0,
                row.YouTubeLikes ?? 0,
                row.YouTubeCommentCount,
                [.. row.Chapters.Select(c => new YouTubeChapter(c.Start, c.End, c.Title))],
                row.YouTubeThumbnailPath);
        return new VideoFacts(row.Id, row.ChannelId, status, refreshed);
    }

    /// <summary>What TonTuyau read on YouTube; null when it never checked the channel.</summary>
    public static ChannelFacts? FactsOf(ChannelRow row)
    {
        if (row.YouTubeState is null)
        {
            return null;
        }

        YouTubeStatus status = new(
            row.YouTubeState.Value,
            row.YouTubeCheckedAt,
            row.YouTubeReason,
            row.YouTubeRefreshedAt);
        RefreshedChannel? refreshed = row.YouTubeRefreshedAt is null || row.YouTubeName is null
            ? null
            : new RefreshedChannel(
                row.YouTubeName,
                row.YouTubeDescription ?? "",
                row.YouTubeSubscribers ?? 0,
                row.YouTubeTags,
                row.YouTubeThumbPath,
                row.YouTubeBannerPath);
        return new ChannelFacts(row.Id, status, refreshed);
    }

    public static StoredVideo ToStored(VideoRow row) =>
        new(ToVideo(row), row.MediaMissing, ToVersions(row.ThumbnailArt));

    public static StoredChannel ToStored(ChannelRow row) =>
        new(ToChannel(row), ToVersions(row.ThumbArt), ToVersions(row.BannerArt));

    public static VideoSummary ToSummary(VideoRow row)
    {
        Video video = ToVideo(row);
        return new VideoSummary(
            video.Id,
            video.Title,
            video.ChannelId,
            video.Type,
            video.Downloaded,
            row.SeenInRun,
            video.ThumbnailPath);
    }

    public static ChannelSummary ToSummary(ChannelRow row)
    {
        Channel channel = ToChannel(row);
        return new ChannelSummary(
            channel.Id,
            channel.Name,
            row.SeenInRun,
            new[] { channel.ThumbPath, channel.BannerPath, channel.TvArtPath }
                .OfType<string>()
                .ToArray());
    }

    public static ArtVersionsRow ToRow(ArtVersions versions) =>
        new() { OriginalWidth = versions.OriginalWidth, Widths = [.. versions.Widths] };

    private static ArtVersions? ToVersions(ArtVersionsRow? row) =>
        row is null ? null : new ArtVersions(row.OriginalWidth, row.Widths);

    private static string Hash<T>(T content) =>
        Convert.ToHexStringLower(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(content)))[..32];
}
