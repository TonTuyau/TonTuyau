using TonTuyau.Application.Library;

namespace TonTuyau.Application.YouTube;

/// <summary>
/// TonTuyau's own facts laid over what TubeArchivist says (1.5): the state, and once TonTuyau refreshed it, the metadata
/// (TubeArchivist still brings the file, its tracks, its dates). Without facts, the state comes from TubeArchivist's
/// flag (<see cref="YouTubeStatus.FromTubeArchivist"/>).
/// </summary>
public static class YouTubeOverlay
{
    public static Video On(Video video, VideoFacts? facts)
    {
        ArgumentNullException.ThrowIfNull(video);
        if (facts is null)
        {
            return video with { YouTube = YouTubeStatus.FromTubeArchivist(video.Active) };
        }

        Video withStatus = video with { YouTube = facts.Status };
        return facts.Refreshed is not { } refreshed
            ? withStatus
            : withStatus with
            {
                Title = refreshed.Title,
                Description = refreshed.Description,
                Tags = refreshed.Tags,
                Categories = refreshed.Categories,
                Stats = video.Stats with { Views = refreshed.Views, Likes = refreshed.Likes },
                ThumbnailPath = refreshed.ThumbnailPath ?? video.ThumbnailPath,
            };
    }

    public static Channel On(Channel channel, ChannelFacts? facts)
    {
        ArgumentNullException.ThrowIfNull(channel);
        if (facts is null)
        {
            return channel with { YouTube = YouTubeStatus.FromTubeArchivist(channel.Active) };
        }

        Channel withStatus = channel with { YouTube = facts.Status };
        return facts.Refreshed is not { } refreshed
            ? withStatus
            : withStatus with
            {
                Name = refreshed.Name,
                Description = refreshed.Description,
                Subscribers = refreshed.Subscribers,
                Tags = refreshed.Tags,
                ThumbPath = refreshed.ThumbPath ?? channel.ThumbPath,
                BannerPath = refreshed.BannerPath ?? channel.BannerPath,
            };
    }
}
