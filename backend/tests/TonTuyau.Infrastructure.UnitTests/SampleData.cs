using TonTuyau.Application.Library;
using TonTuyau.Application.YouTube;

namespace TonTuyau.Infrastructure.UnitTests;

internal static class SampleData
{
    public static Video Video(string id = "abcdefghijk") => new(
        Id: id,
        ChannelId: "UCaaaaaaaaaaaaaaaaaaaaaa",
        ChannelName: "Chaîne d'essai",
        Title: "Recette de la crème brûlée",
        Description: "Une vidéo de test.",
        Type: VideoType.Video,
        Published: new DateTimeOffset(2024, 5, 6, 7, 8, 9, TimeSpan.Zero),
        Downloaded: new DateTimeOffset(2025, 1, 2, 3, 4, 5, TimeSpan.Zero),
        Duration: TimeSpan.FromSeconds(1914.5),
        MediaSize: 1_313_668_754,
        MediaPath: $"UCaaaaaaaaaaaaaaaaaaaaaa/{id}.mp4",
        MediaPathCorrected: false,
        ThumbnailPath: $"/cache/videos/{char.ToLowerInvariant(id[0])}/{id}.jpg",
        Tags: ["cuisine", "dessert"],
        Categories: ["Howto & Style"],
        Stats: new VideoStats(74198, 3166, 0),
        Tracks:
        [
            new MediaTrack(MediaTrackKind.Video, "av1", 5_370_710, 3840, 2160),
            new MediaTrack(MediaTrackKind.Audio, "opus", 110_978, null, null),
        ],
        YouTube: YouTubeStatus.NotChecked);

    public static Channel Channel(string id = "UCaaaaaaaaaaaaaaaaaaaaaa") => new(
        Id: id,
        Name: "Chaîne d'essai",
        Description: "Description",
        Subscribers: 224_000,
        Tags: ["cuisine"],
        ThumbPath: $"/cache/channels/{id}_thumb.jpg",
        BannerPath: $"/cache/channels/{id}_banner.jpg",
        TvArtPath: null,
        YouTube: YouTubeStatus.NotChecked);
}
