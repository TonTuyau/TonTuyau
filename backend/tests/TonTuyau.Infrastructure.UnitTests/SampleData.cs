using TonTuyau.Application.Library;

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
        Active: true,
        Published: new DateTimeOffset(2024, 5, 6, 7, 8, 9, TimeSpan.Zero),
        Downloaded: new DateTimeOffset(2025, 1, 2, 3, 4, 5, TimeSpan.Zero),
        LastRefreshed: new DateTimeOffset(2026, 9, 30, 12, 0, 0, TimeSpan.Zero),
        Duration: TimeSpan.FromSeconds(1914.5),
        MediaSize: 1_313_668_754,
        MediaPath: $"UCaaaaaaaaaaaaaaaaaaaaaa/{id}.mp4",
        MediaPathCorrected: false,
        ThumbnailPath: $"/cache/videos/{char.ToLowerInvariant(id[0])}/{id}.jpg",
        Tags: ["cuisine", "dessert"],
        Categories: ["Howto & Style"],
        PlaylistIds: [],
        Stats: new VideoStats(74198, 3166, 0),
        Tracks:
        [
            new MediaTrack(MediaTrackKind.Video, "av1", 5_370_710, 3840, 2160),
            new MediaTrack(MediaTrackKind.Audio, "opus", 110_978, null, null),
        ]);

    public static Channel Channel(string id = "UCaaaaaaaaaaaaaaaaaaaaaa") => new(
        Id: id,
        Name: "Chaîne d'essai",
        Description: "Description",
        Subscribers: 224_000,
        Active: true,
        LastRefreshed: new DateTimeOffset(2026, 10, 1, 7, 17, 7, TimeSpan.Zero),
        Tags: ["cuisine"],
        Tabs: [VideoType.Video, VideoType.Short],
        ThumbPath: $"/cache/channels/{id}_thumb.jpg",
        BannerPath: $"/cache/channels/{id}_banner.jpg",
        TvArtPath: null);
}
