using TonTuyau.Application.Library;
using TonTuyau.Application.YouTube;

namespace TonTuyau.Application.UnitTests.YouTube;

public sealed class YouTubeOverlayTests
{
    private static readonly DateTimeOffset At = new(2026, 10, 1, 8, 0, 0, TimeSpan.Zero);

    private static readonly Video FromTubeArchivist = new(
        Id: "abcdefghijk",
        ChannelId: "UCabcdefghijklmnopqrstuv",
        ChannelName: "Chaîne",
        Title: "TubeArchivist's title",
        Description: "TubeArchivist's description",
        Type: VideoType.Video,
        Active: true,
        Published: At,
        Downloaded: At,
        LastRefreshed: At,
        Duration: TimeSpan.FromMinutes(3),
        MediaSize: 1000,
        MediaPath: "UCabcdefghijklmnopqrstuv/abcdefghijk.mp4",
        MediaPathCorrected: false,
        ThumbnailPath: "/cache/videos/a/abcdefghijk.jpg",
        Tags: ["ta"],
        Categories: ["Music"],
        PlaylistIds: [],
        Stats: new VideoStats(Views: 10, Likes: 1, Dislikes: 2),
        Tracks: []);

    private static readonly Channel ChannelFromTubeArchivist = new(
        Id: "UCabcdefghijklmnopqrstuv",
        Name: "TubeArchivist's name",
        Description: "TubeArchivist's description",
        Subscribers: 5,
        Active: true,
        LastRefreshed: At,
        Tags: ["ta"],
        Tabs: [VideoType.Video],
        ThumbPath: "/cache/channels/thumb.jpg",
        BannerPath: "/cache/channels/banner.jpg",
        TvArtPath: null);

    [Theory]
    [InlineData(true, YouTubeState.NotChecked)]
    [InlineData(false, YouTubeState.RetryLater)]
    public void Without_facts_TubeArchivist_s_flag_is_never_gone(bool active, YouTubeState expected)
    {
        Video video = YouTubeOverlay.On(FromTubeArchivist with { Active = active }, facts: null);

        Assert.Equal(expected, video.YouTube!.State);
        Assert.False(video.GoneFromYouTube);
        Assert.Equal("TubeArchivist's title", video.Title);
    }

    [Fact]
    public void A_state_without_refresh_keeps_TubeArchivist_s_metadata()
    {
        YouTubeStatus gone = new(YouTubeState.Gone, At, "Video unavailable", RefreshedAt: null);

        Video video = YouTubeOverlay.On(
            FromTubeArchivist,
            new VideoFacts(FromTubeArchivist.Id, FromTubeArchivist.ChannelId, gone, Refreshed: null));

        Assert.Same(gone, video.YouTube);
        Assert.True(video.GoneFromYouTube);
        Assert.Equal(FromTubeArchivist with { YouTube = gone }, video);
    }

    [Fact]
    public void A_refreshed_video_takes_TonTuyau_s_metadata_and_keeps_TubeArchivist_s_file()
    {
        YouTubeStatus onYouTube = new(YouTubeState.OnYouTube, At, Reason: null, RefreshedAt: At);
        RefreshedVideo refreshed = new(
            "YouTube's title",
            "Sa chaîne",
            "Its description",
            ["yt"],
            ["Education"],
            Views: 999,
            Likes: 42,
            CommentCount: 7,
            ThumbnailPath: null);

        Video video = YouTubeOverlay.On(
            FromTubeArchivist,
            new VideoFacts(FromTubeArchivist.Id, FromTubeArchivist.ChannelId, onYouTube, refreshed));

        Assert.Equal("YouTube's title", video.Title);
        Assert.Equal("Sa chaîne", video.ChannelName);
        Assert.Equal("Its description", video.Description);
        Assert.Equal(["yt"], video.Tags);
        Assert.Equal(["Education"], video.Categories);
        Assert.Equal(new VideoStats(Views: 999, Likes: 42, Dislikes: 2), video.Stats);
        Assert.Equal(FromTubeArchivist.ThumbnailPath, video.ThumbnailPath);
        Assert.Equal(FromTubeArchivist.MediaPath, video.MediaPath);
    }

    [Fact]
    public void A_refreshed_thumbnail_replaces_TubeArchivist_s()
    {
        YouTubeStatus onYouTube = new(YouTubeState.OnYouTube, At, Reason: null, RefreshedAt: At);
        RefreshedVideo refreshed = new("T", "C", "D", [], [], 1, 1, null, "/art/videos/abcdefghijk.jpg");

        Video video = YouTubeOverlay.On(
            FromTubeArchivist,
            new VideoFacts(FromTubeArchivist.Id, FromTubeArchivist.ChannelId, onYouTube, refreshed));

        Assert.Equal("/art/videos/abcdefghijk.jpg", video.ThumbnailPath);
    }

    [Fact]
    public void A_refreshed_channel_takes_TonTuyau_s_metadata_and_pictures_it_has()
    {
        YouTubeStatus onYouTube = new(YouTubeState.OnYouTube, At, Reason: null, RefreshedAt: At);
        RefreshedChannel refreshed = new("YouTube's name", "YouTube's description", 77, ["yt"], "/art/thumb.jpg", null);

        Channel channel = YouTubeOverlay.On(
            ChannelFromTubeArchivist,
            new ChannelFacts(ChannelFromTubeArchivist.Id, onYouTube, refreshed));

        Assert.Equal("YouTube's name", channel.Name);
        Assert.Equal(77, channel.Subscribers);
        Assert.Equal("/art/thumb.jpg", channel.ThumbPath);
        Assert.Equal(ChannelFromTubeArchivist.BannerPath, channel.BannerPath);
        Assert.False(channel.GoneFromYouTube);
    }

    [Fact]
    public void A_channel_recorded_only_for_its_videos_still_follows_TubeArchivist_s_flag()
    {
        YouTubeStatus notChecked = new(YouTubeState.NotChecked, null, null, null);

        Channel channel = YouTubeOverlay.On(
            ChannelFromTubeArchivist with { Active = false },
            new ChannelFacts(ChannelFromTubeArchivist.Id, notChecked, Refreshed: null));

        Assert.Equal(YouTubeState.RetryLater, channel.YouTube!.State);
    }

    [Fact]
    public void Without_facts_an_inactive_channel_is_retry_later()
    {
        Channel channel = YouTubeOverlay.On(ChannelFromTubeArchivist with { Active = false }, facts: null);

        Assert.Equal(YouTubeState.RetryLater, channel.YouTube!.State);
        Assert.False(channel.GoneFromYouTube);
    }
}
