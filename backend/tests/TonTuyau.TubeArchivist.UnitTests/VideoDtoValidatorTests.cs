using System.Text.Json;
using TonTuyau.Application.Validation;
using TonTuyau.TubeArchivist.Dtos;
using TonTuyau.TubeArchivist.UnitTests.Support;
using TonTuyau.TubeArchivist.Validation;

namespace TonTuyau.TubeArchivist.UnitTests;

public sealed class VideoDtoValidatorTests
{
    private readonly VideoDtoValidator _validator = new();

    public enum Broken
    {
        IdMissing,
        IdTooShort,
        IdWithSlash,
        ChannelMissing,
        ChannelIdBad,
        ChannelNameBlank,
        TitleBlank,
        TypeUnknown,
        ActiveMissing,
        PublishedBad,
        DownloadedZero,
        RefreshBad,
        SizeNegative,
        DurationMissing,
        DurationNegative,
        MediaMissing,
        MediaTraversal,
        MediaEncodedTraversal,
        MediaAbsoluteElsewhere,
        MediaOtherVideo,
        MediaBadChannelFolder,
        MediaBadExtension,
        MediaTrailingNewline,
        ThumbMissing,
        ThumbTraversal,
        ThumbOtherVideo,
        ThumbNested,
        StreamsMissing,
        StreamWithoutCodec,
    }

    [Fact]
    public void Recorded_video_is_valid()
    {
        _validator.Validate(ValidVideo());
    }

    [Fact]
    public void Every_recorded_video_in_every_list_fixture_is_valid()
    {
        string[] fixtures = new[] {
            "videos-downloaded-desc-page-1",
            "videos-downloaded-desc-page-2",
            "videos-downloaded-desc-page-100",
            "videos-shorts-page-1",
            "videos-streams-page-1",
            "videos-small-channel-page-1" };
        int count = 0;
        foreach (string? name in fixtures)
        {
            foreach (JsonElement item in FixtureHandler.Get(name).Json.GetProperty("data").EnumerateArray())
            {
                _validator.Validate(item.Deserialize<VideoDto>(TaJson.Options)!);
                count++;
            }
        }

        Assert.True(count > 400);
    }

    [Theory, CombinatorialData]
    public void Each_broken_field_is_reported_by_name(Broken broken)
    {
        (VideoDto? dto, string? field) = Break(ValidVideo(), broken);

        ValidationException ex = Assert.Throws<ValidationException>(() => _validator.Validate(dto));

        Assert.Contains(ex.Errors, e => e.Field == field);
    }

    [Theory, CombinatorialData]
    public void Unrelated_fields_are_not_blamed(Broken broken)
    {
        (VideoDto? dto, string? field) = Break(ValidVideo(), broken);

        ValidationException ex = Assert.Throws<ValidationException>(() => _validator.Validate(dto));

        // A broken id also makes the id-dependent path checks impossible, but never blames unrelated fields.
        HashSet<string> allowed = new() { field, "media_url", "vid_thumb_url", "channel.channel_name" };
        Assert.All(ex.Errors, e => Assert.Contains(e.Field, allowed));
    }

    [Fact]
    public void Media_in_another_channel_folder_is_accepted_for_correction()
    {
        _validator.Validate(
            ValidVideo() with { MediaUrl = $"/youtube/UCzzzzzzzzzzzzzzzzzzzzzz/{ValidVideo().YoutubeId}.mp4" });
    }

    [Fact]
    public void Optional_fields_may_be_missing()
    {
        VideoDto dto = ValidVideo() with
        {
            Description = null,
            VidLastRefresh = null,
            Tags = null,
            Category = null,
            Playlist
            = null,
            Stats = null,
            Streams = []
        };

        _validator.Validate(dto);
    }

    [Fact]
    public void Unknown_stream_types_are_not_an_error()
    {
        VideoDto dto = ValidVideo() with { Streams = [new StreamDto("subtitle", null, null, null, null)] };

        _validator.Validate(dto);
    }

    [Theory]
    [InlineData("2022-12-06T15:00:14+00:00")]
    [InlineData("2022-12-06T15:00:14Z")]
    [InlineData("2022-12-06")]
    public void Published_accepts_iso_dates(string published)
    {
        _validator.Validate(ValidVideo() with { Published = published });
    }

    private static readonly StreamDto BlankCodecStream = new("video", " ", 1, 1, 1);

    internal static VideoDto ValidVideo() =>
        FixtureHandler.Get("video-inactive").Json.Deserialize<VideoDto>(TaJson.Options)!;

    private static (VideoDto Dto, string Field) Break(VideoDto v, Broken broken)
    {
        string id = v.YoutubeId!;
        string channel = v.Channel!.ChannelId!;
        return broken switch
        {
            Broken.IdMissing => (v with { YoutubeId = null }, "youtube_id"),
            Broken.IdTooShort => (v with { YoutubeId = "abc" }, "youtube_id"),
            Broken.IdWithSlash => (v with { YoutubeId = "abc/def/ghi" }, "youtube_id"),
            Broken.ChannelMissing => (v with { Channel = null }, "channel.channel_id"),
            Broken.ChannelIdBad => (
                v with
                {
                    Channel = v
                    .Channel with
                    { ChannelId = "UC_too_short" }
                },
                "channel.channel_id"),
            Broken.ChannelNameBlank => (
                v with
                {
                    Channel = v
                    .Channel with
                    { ChannelName = " " }
                },
                "channel.channel_name"),
            Broken.TitleBlank => (v with { Title = "  " }, "title"),
            Broken.TypeUnknown => (v with { VidType = "live" }, "vid_type"),
            Broken.ActiveMissing => (v with { Active = null }, "active"),
            Broken.PublishedBad => (v with { Published = "yesterday" }, "published"),
            Broken.DownloadedZero => (v with { DateDownloaded = 0 }, "date_downloaded"),
            Broken.RefreshBad => (v with { VidLastRefresh = "soon" }, "vid_last_refresh"),
            Broken.SizeNegative => (v with { MediaSize = -1 }, "media_size"),
            Broken.DurationMissing => (v with { Player = null }, "player.duration"),
            Broken.DurationNegative => (v with { Player = new PlayerDto(-3) }, "player.duration"),
            Broken.MediaMissing => (v with { MediaUrl = null }, "media_url"),
            Broken.MediaTraversal => (v with { MediaUrl = $"/youtube/{channel}/../../etc/{id}.mp4" }, "media_url"),
            Broken.MediaEncodedTraversal => (v with { MediaUrl = $"/youtube/{channel}/%2e%2e/{id}.mp4" }, "media_url"),
            Broken.MediaAbsoluteElsewhere => (v with { MediaUrl = $"/etc/{channel}/{id}.mp4" }, "media_url"),
            Broken.MediaOtherVideo => (v with { MediaUrl = $"/youtube/{channel}/zzzzzzzzzzz.mp4" }, "media_url"),
            Broken.MediaBadChannelFolder => (v with { MediaUrl = $"/youtube/not-a-channel/{id}.mp4" }, "media_url"),
            Broken.MediaTrailingNewline => (v with { MediaUrl = $"/youtube/{channel}/{id}.mp4\n" }, "media_url"),
            Broken.MediaBadExtension => (v with { MediaUrl = $"/youtube/{channel}/{id}.sh" }, "media_url"),
            Broken.ThumbMissing => (v with { VidThumbUrl = null }, "vid_thumb_url"),
            Broken.ThumbTraversal => (v with { VidThumbUrl = $"/cache/videos/../../{id}.jpg" }, "vid_thumb_url"),
            Broken.ThumbOtherVideo => (v with { VidThumbUrl = "/cache/videos/z/zzzzzzzzzzz.jpg" }, "vid_thumb_url"),
            Broken.ThumbNested => (v with { VidThumbUrl = $"/cache/videos/a/b/{id}.jpg" }, "vid_thumb_url"),
            Broken.StreamsMissing => (v with { Streams = null }, "streams"),
            Broken.StreamWithoutCodec => (v with { Streams = [BlankCodecStream] }, "streams[0].codec"),
            _ => throw new ArgumentOutOfRangeException(nameof(broken)),
        };
    }
}
