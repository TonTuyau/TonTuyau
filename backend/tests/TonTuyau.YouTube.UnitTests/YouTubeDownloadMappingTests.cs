using TonTuyau.Application.Library;
using TonTuyau.YouTube.Dtos;

namespace TonTuyau.YouTube.UnitTests;

/// <summary>What a download's info dict becomes (2.0): its tracks, named as ffprobe names codecs, and its type.</summary>
public sealed class YouTubeDownloadMappingTests
{
    private static VideoInfoDto Info(
        string? liveStatus = null,
        string? mediaType = null,
        IReadOnlyList<FormatDto>? formats = null,
        string? vcodec = null,
        string? acodec = null) =>
        new(
            "abcdefghijk",
            "Titre",
            null,
            "UCaaaaaaaaaaaaaaaaaaaaaa",
            "Chaîne",
            null,
            null,
            60,
            null,
            null,
            null,
            null,
            null,
            liveStatus,
            null,
            null,
            null,
            null,
            mediaType,
            vcodec,
            acodec,
            1920,
            1080,
            2500.4,
            formats);

    [Theory]
    [InlineData("vp09.00.40.08", "vp9")]
    [InlineData("vp9", "vp9")]
    [InlineData("av01.0.08M.08", "av1")]
    [InlineData("avc1.640028", "h264")]
    [InlineData("hev1.1.6.L120.90", "hevc")]
    [InlineData("mp4a.40.2", "aac")]
    [InlineData("opus", "opus")]
    [InlineData("none", null)]
    [InlineData("", null)]
    [InlineData(null, null)]
    public void Codecs_are_named_as_ffprobe_names_them(string? youTube, string? expected) =>
        Assert.Equal(expected, YouTubeClient.CodecName(youTube));

    [Fact]
    public void Merged_formats_give_a_video_and_an_audio_track()
    {
        FormatDto[] formats =
        [
            new("vp09.00.40.08", "none", 1920, 1080, 2000.6),
            new("none", "opus", null, null, 128),
        ];

        List<MediaTrack> tracks = YouTubeClient.TracksOf(Info(formats: formats));

        Assert.Equal(
            [
                new MediaTrack(MediaTrackKind.Video, "vp9", 2_000_600, 1920, 1080),
                new MediaTrack(MediaTrackKind.Audio, "opus", 128_000, null, null),
            ],
            tracks);
    }

    [Fact]
    public void A_single_format_gives_its_own_tracks()
    {
        List<MediaTrack> tracks = YouTubeClient.TracksOf(Info(vcodec: "avc1.64001F", acodec: "mp4a.40.2"));

        Assert.Equal(
            [
                new MediaTrack(MediaTrackKind.Video, "h264", 2_500_400, 1920, 1080),
                new MediaTrack(MediaTrackKind.Audio, "aac", 2_500_400, null, null),
            ],
            tracks);
    }

    [Theory]
    [InlineData("not_live", null, VideoType.Video)]
    [InlineData("was_live", null, VideoType.Stream)]
    [InlineData("post_live", null, VideoType.Stream)]
    [InlineData(null, "livestream", VideoType.Stream)]
    [InlineData(null, "short", VideoType.Short)]
    [InlineData(null, "video", VideoType.Video)]
    [InlineData(null, null, VideoType.Video)]
    public void The_type_is_YouTubes(string? liveStatus, string? mediaType, VideoType expected) =>
        Assert.Equal(expected, YouTubeClient.TypeOf(Info(liveStatus, mediaType)));
}
