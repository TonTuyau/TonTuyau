using TonTuyau.Application.Library;
using TonTuyau.TubeArchivist.Dtos;
using TonTuyau.TubeArchivist.Mapping;

namespace TonTuyau.TubeArchivist.UnitTests;

public sealed class TaMapperTests
{
    [Theory, CombinatorialData]
    public void Video_type_is_mapped([CombinatorialValues("videos", "shorts", "streams")] string vidType)
    {
        Video video = TaMapper.ToVideo(VideoDtoValidatorTests.ValidVideo() with { VidType = vidType });

        Assert.Equal(
            vidType switch { "videos" => VideoType.Video, "shorts" => VideoType.Short, _ => VideoType.Stream },
            video.Type);
    }

    [Fact]
    public void Blank_tags_are_dropped_and_missing_lists_become_empty()
    {
        Video video = TaMapper.ToVideo(
            VideoDtoValidatorTests
                .ValidVideo() with
            { Tags = ["a", null, " ", "b"], Category = null, Playlist = null });

        Assert.Equal(["a", "b"], video.Tags);
        Assert.Empty(video.Categories);
        Assert.Empty(video.PlaylistIds);
    }

    [Fact]
    public void Missing_description_and_stats_get_neutral_values()
    {
        Video video = TaMapper.ToVideo(
            VideoDtoValidatorTests.ValidVideo() with { Description = null, Stats = null, VidLastRefresh = null });

        Assert.Equal("", video.Description);
        Assert.Equal(
            new VideoStats(0, 0, 0),
            video.Stats);
        Assert.Null(video.LastRefreshed);
    }

    [Fact]
    public void Only_video_and_audio_tracks_are_kept()
    {
        List<StreamDto?> streams =
        [
            new StreamDto("video", "vp9", 1000, 1920, 1080),
            new StreamDto("audio", "opus", 128, null, null),
            new StreamDto("subtitle", "webvtt", null, null, null),
            null,
        ];
        MediaTrack[] expected =
        [
            new MediaTrack(MediaTrackKind.Video, "vp9", 1000, 1920, 1080),
            new MediaTrack(MediaTrackKind.Audio, "opus", 128, null, null),
        ];

        Video video = TaMapper.ToVideo(VideoDtoValidatorTests.ValidVideo() with { Streams = streams });

        Assert.Equal(expected, video.Tracks);
    }

    [Fact]
    public void Media_path_is_relative_to_the_share_root()
    {
        VideoDto valid = VideoDtoValidatorTests.ValidVideo();
        string? channelId = valid.Channel!.ChannelId;
        VideoDto dto = valid with { MediaUrl = $"/youtube/{channelId}/{valid.YoutubeId}.webm" };

        Assert.Equal($"{channelId}/{valid.YoutubeId}.webm", TaMapper.ToVideo(dto).MediaPath);
    }

    [Theory, CombinatorialData]
    public void Media_path_always_uses_the_video_own_channel_and_flags_corrections(bool otherFolder)
    {
        VideoDto valid = VideoDtoValidatorTests.ValidVideo();
        string? folder = otherFolder ? "UCzzzzzzzzzzzzzzzzzzzzzz" : valid.Channel!.ChannelId;

        Video video = TaMapper.ToVideo(valid with { MediaUrl = $"/youtube/{folder}/{valid.YoutubeId}.mkv" });

        Assert.Equal($"{valid.Channel!.ChannelId}/{valid.YoutubeId}.mkv", video.MediaPath);
        Assert.Equal(otherFolder, video.MediaPathCorrected);
    }

    [Fact]
    public void Unknown_channel_tabs_are_ignored()
    {
        Channel channel = TaMapper.ToChannel(new ChannelDto(
            "UCaaaaaaaaaaaaaaaaaaaaaa",
            "n",
            null,
            true,
            null,
            null,
            null,
            ["videos", "live", "streams", "podcasts"],
            null,
            "",
            null));

        Assert.Equal([VideoType.Video, VideoType.Stream], channel.Tabs);
        Assert.Null(channel.BannerPath);
        Assert.Equal(0, channel.Subscribers);
    }
}
