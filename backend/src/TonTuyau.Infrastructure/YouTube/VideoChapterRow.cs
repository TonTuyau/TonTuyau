namespace TonTuyau.Infrastructure.YouTube;

/// <summary>A chapter of a video, in <c>youtube_videos.chapters</c> (JSON, 1.5): its start and end in seconds, its title.</summary>
public sealed class VideoChapterRow
{
    public double Start { get; set; }

    public double End { get; set; }

    public required string Title { get; set; }
}
