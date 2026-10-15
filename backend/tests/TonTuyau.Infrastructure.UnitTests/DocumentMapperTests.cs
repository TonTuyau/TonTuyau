using System.Text.Json;
using TonTuyau.Application.Library;
using TonTuyau.Application.YouTube;
using TonTuyau.Infrastructure.Elasticsearch.Documents;

namespace TonTuyau.Infrastructure.UnitTests;

public sealed class DocumentMapperTests
{
    public enum VideoChange
    {
        Title,
        Description,
        ChannelName,
        Tags,
        Type,
        Active,
        Published,
        Duration,
        Views,
        MediaPath,
        Corrected,
        TrackCodec,
        Thumbnail,
    }

    [Theory, CombinatorialData]
    public void Videos_round_trip_through_the_document_and_its_json(
        VideoType type,
        bool active,
        bool corrected,
        bool refreshed)
    {
        Video video = SampleData.Video() with
        {
            Type = type,
            Active = active,
            MediaPathCorrected
            = corrected,
            LastRefreshed = refreshed ? DateTimeOffset.UnixEpoch : null,
            YouTube = StatusOf(active),
        };

        string json = JsonSerializer.Serialize(DocumentMapper.ToDocument(video), StoreJson.Options);
        Video back = DocumentMapper.ToVideo(JsonSerializer.Deserialize<VideoDocument>(json, StoreJson.Options)!);

        Assert.Equal(Canonical(video), Canonical(back));
    }

    [Theory, CombinatorialData]
    public void Channels_round_trip_through_the_document_and_its_json(bool active, bool withArt)
    {
        Channel channel = SampleData.Channel() with
        {
            Active = active,
            BannerPath = withArt
            ? "/cache/channels/UCaaaaaaaaaaaaaaaaaaaaaa_banner.jpg"
            : null,
            YouTube = StatusOf(active),
        };

        string json = JsonSerializer.Serialize(DocumentMapper.ToDocument(channel), StoreJson.Options);
        Channel back = DocumentMapper.ToChannel(JsonSerializer.Deserialize<ChannelDocument>(json, StoreJson.Options)!);

        Assert.Equal(Canonical(channel), Canonical(back));
    }

    /// <summary>A document always has a state (the sync lays TonTuyau's facts first): every field filled, or none.</summary>
    private static YouTubeStatus StatusOf(bool active) =>
        active
            ? new YouTubeStatus(YouTubeState.OnYouTube, DateTimeOffset.UnixEpoch, null, DateTimeOffset.UnixEpoch)
            : new YouTubeStatus(YouTubeState.Gone, DateTimeOffset.UnixEpoch, "Video unavailable", RefreshedAt: null);

    [Fact]
    public void Same_content_gives_the_same_hash()
    {
        Assert.Equal(
            DocumentMapper.ToDocument(SampleData.Video()).ContentHash,
            DocumentMapper.ToDocument(SampleData.Video()).ContentHash);
    }

    [Theory, CombinatorialData]
    public void Any_content_change_changes_the_hash(VideoChange change)
    {
        Video v = SampleData.Video();
        Video changed = change switch
        {
            VideoChange.Title => v with { Title = v.Title + "!" },
            VideoChange.Description => v with { Description = "" },
            VideoChange.ChannelName => v with { ChannelName = "Renamed" },
            VideoChange.Tags => v with { Tags = ["cuisine"] },
            VideoChange.Type => v with { Type = VideoType.Stream },
            VideoChange.Active => v with { Active = false },
            VideoChange.Published => v with { Published = v.Published.AddSeconds(1) },
            VideoChange.Duration => v with { Duration = v.Duration + TimeSpan.FromSeconds(1) },
            VideoChange.Views => v with { Stats = v.Stats with { Views = v.Stats.Views + 1 } },
            VideoChange.MediaPath => v with { MediaPath = "UCaaaaaaaaaaaaaaaaaaaaaa/abcdefghijk.webm" },
            VideoChange.Corrected => v with { MediaPathCorrected = true },
            VideoChange.TrackCodec => v with { Tracks = [v.Tracks[0] with { Codec = "vp9" }, v.Tracks[1]] },
            VideoChange.Thumbnail => v with { ThumbnailPath = "/cache/videos/a/abcdefghijk.webp" },
            _ => throw new ArgumentOutOfRangeException(nameof(change)),
        };

        Assert.NotEqual(DocumentMapper.ToDocument(v).ContentHash, DocumentMapper.ToDocument(changed).ContentHash);
    }

    [Fact]
    public void Bookkeeping_is_not_content()
    {
        VideoDocument doc = DocumentMapper.ToDocument(SampleData.Video());

        Assert.Equal(doc.ContentHash, StoreJson.ContentHash(doc with { ContentHash = null }));
        Assert.Null(doc.SeenInRun);
    }

    [Fact]
    public void Art_records_are_bookkeeping_and_left_out_of_the_json_when_unset()
    {
        VideoDocument doc = DocumentMapper.ToDocument(SampleData.Video());
        string plain = JsonSerializer.Serialize(doc, StoreJson.Options);

        Assert.DoesNotContain("thumbnail_art", plain, StringComparison.Ordinal);
        Assert.Equal(doc.ContentHash, StoreJson.ContentHash(doc with { ContentHash = null }));

        VideoDocument recorded = doc with { ThumbnailArt = new ArtDocument(1280, [360, 720]) };
        Assert.Contains(
            "\"thumbnail_art\":{\"width\":1280,\"sizes\":[360,720]}",
            JsonSerializer.Serialize(recorded, StoreJson.Options),
            StringComparison.Ordinal);
    }

    [Fact]
    public void Art_records_map_both_ways()
    {
        ArtVersions art = new(900, [48, 96]);

        Assert.Equal(art, DocumentMapper.ToArt(DocumentMapper.ToDocument(art)));
        Assert.Null(DocumentMapper.ToArt(null));
    }

    [Theory]
    [InlineData("videos")]
    [InlineData("Video")]
    [InlineData("")]
    public void Unknown_types_in_the_index_are_an_error(string name)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => DocumentMapper.ParseType(name));
    }

    private static string Canonical<T>(T value) => JsonSerializer.Serialize(value);
}
