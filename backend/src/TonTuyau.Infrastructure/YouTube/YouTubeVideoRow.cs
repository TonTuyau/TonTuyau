using TonTuyau.Application.YouTube;

namespace TonTuyau.Infrastructure.YouTube;

/// <summary>
/// A row of <c>youtube_videos</c> (1.5): what TonTuyau itself knows of a video, by its YouTube id; the source of truth
/// Elasticsearch is rebuilt with. The metadata columns are set once TonTuyau refreshed the video.
/// </summary>
public sealed class YouTubeVideoRow
{
    public required string VideoId { get; set; }

    /// <summary>Stored as its name (<c>Gone</c>).</summary>
    public YouTubeState State { get; set; }

    public DateTimeOffset? CheckedAt { get; set; }

    public string? Reason { get; set; }

    public DateTimeOffset? RefreshedAt { get; set; }

    public string? Title { get; set; }

    public string? ChannelName { get; set; }

    public string? Description { get; set; }

    public List<string> Tags { get; set; } = [];

    public List<string> Categories { get; set; } = [];

    public long? Views { get; set; }

    public long? Likes { get; set; }

    public long? CommentCount { get; set; }

    /// <summary>The thumbnail TonTuyau stored in its art volume (its path there); null: TubeArchivist's stays.</summary>
    public string? ThumbnailPath { get; set; }

    public static explicit operator VideoFacts(YouTubeVideoRow row)
    {
        ArgumentNullException.ThrowIfNull(row);
        YouTubeStatus status = new(row.State, row.CheckedAt, row.Reason, row.RefreshedAt);
        RefreshedVideo? refreshed = row.RefreshedAt is null || row.Title is null
            ? null
            : new RefreshedVideo(
                row.Title,
                row.ChannelName ?? "",
                row.Description ?? "",
                row.Tags,
                row.Categories,
                row.Views ?? 0,
                row.Likes ?? 0,
                row.CommentCount,
                row.ThumbnailPath);
        return new VideoFacts(row.VideoId, status, refreshed);
    }
}
