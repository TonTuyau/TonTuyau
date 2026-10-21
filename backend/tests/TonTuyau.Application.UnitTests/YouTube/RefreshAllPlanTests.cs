using TonTuyau.Application.YouTube;

namespace TonTuyau.Application.UnitTests.YouTube;

public sealed class RefreshAllPlanTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 21, 12, 0, 0, TimeSpan.Zero);

    private static LibraryCheck Channel(string id, YouTubeState state = YouTubeState.NotChecked, int? daysAgo = null) =>
        new(id, null, state, daysAgo is null ? null : Now.AddDays(-daysAgo.Value));

    private static LibraryCheck Video(
        string id,
        string channel,
        YouTubeState state = YouTubeState.NotChecked,
        int? daysAgo = null) =>
        new(id, channel, state, daysAgo is null ? null : Now.AddDays(-daysAgo.Value));

    [Fact]
    public void Each_channel_then_its_videos_the_least_recently_read_first()
    {
        LibraryCheck[] checks =
        [
            Channel("UCa", YouTubeState.OnYouTube, daysAgo: 30),
            Video("a1", "UCa", YouTubeState.OnYouTube, daysAgo: 20),
            Video("a2", "UCa", YouTubeState.OnYouTube, daysAgo: 40),
            Channel("UCb", YouTubeState.OnYouTube, daysAgo: 10),
            Video("b1", "UCb"),
        ];

        (IReadOnlyList<string> work, int skipped) = RefreshAllPlan.Order(checks, Now, days: 7);

        // UCb holds a video never read: the oldest read of all, so it comes first.
        Assert.Equal(["UCb", "b1", "UCa", "a2", "a1"], work);
        Assert.Equal(0, skipped);
    }

    [Fact]
    public void What_was_read_lately_and_what_is_gone_are_skipped_what_is_to_retry_is_not()
    {
        LibraryCheck[] checks =
        [
            Channel("UCa", YouTubeState.OnYouTube, daysAgo: 2),
            Video("fresh", "UCa", YouTubeState.Unlisted, daysAgo: 3),
            Video("private", "UCa", YouTubeState.Private, daysAgo: 1),
            Video("gone", "UCa", YouTubeState.Gone, daysAgo: 30),
            Video("retry", "UCa", YouTubeState.RetryLater, daysAgo: 1),
            Video("old", "UCa", YouTubeState.OnYouTube, daysAgo: 8),
            Channel("UCdone", YouTubeState.OnYouTube, daysAgo: 1),
            Video("done", "UCdone", YouTubeState.OnYouTube, daysAgo: 1),
        ];

        (IReadOnlyList<string> work, int skipped) = RefreshAllPlan.Order(checks, Now, days: 7);

        // UCa itself was read lately: only its videos to read; UCdone has nothing left.
        Assert.Equal(["retry", "old"], work);
        Assert.Equal(6, skipped);
    }

    [Fact]
    public void At_zero_days_everything_but_the_gone_is_read()
    {
        LibraryCheck[] checks =
        [
            Channel("UCa", YouTubeState.OnYouTube, daysAgo: 0),
            Video("today", "UCa", YouTubeState.OnYouTube, daysAgo: 0),
            Video("gone", "UCa", YouTubeState.Gone, daysAgo: 0),
        ];

        (IReadOnlyList<string> work, int skipped) = RefreshAllPlan.Order(checks, Now, days: 0);

        Assert.Equal(["UCa", "today"], work);
        Assert.Equal(1, skipped);
    }
}
