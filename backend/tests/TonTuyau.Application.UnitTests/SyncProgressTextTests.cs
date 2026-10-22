using TonTuyau.Application.Sync;

namespace TonTuyau.Application.UnitTests;

public sealed class SyncProgressTextTests
{
    private static readonly DateTimeOffset Start = new(2026, 10, 7, 9, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData(0, "00:00")]
    [InlineData(42, "00:42")]
    [InlineData(370, "06:10")]
    [InlineData(3599, "59:59")]
    [InlineData(3600, "1:00:00")]
    [InlineData(3723, "1:02:03")]
    [InlineData(36000, "10:00:00")]
    [InlineData(-5, "00:00")] // clocks a hair apart: never a negative time
    public void Elapsed_is_minutes_and_seconds_then_hours_past_an_hour(int seconds, string expected) =>
        Assert.Equal(expected, SyncProgressText.Elapsed(TimeSpan.FromSeconds(seconds)));

    [Fact]
    public void Elapsed_drops_fractions_of_a_second() =>
        Assert.Equal("00:09", SyncProgressText.Elapsed(TimeSpan.FromMilliseconds(9_999)));

    [Theory]
    [InlineData(12, 120, "12 / 120")]
    [InlineData(120, 120, "120 / 120")]
    [InlineData(0, 0, "0 / 0")]
    [InlineData(31_500, 45_339, "31,500 / 45,339")]
    [InlineData(12, null, "12")] // unknown: never guessed
    [InlineData(121, 120, "121")] // more done than the "total": the total is wrong, so it isn't shown
    public void A_total_is_shown_only_when_known_and_possible(int done, int? total, string expected) =>
        Assert.Equal(expected, SyncProgressText.OutOf(done, total));

    [Theory]
    [InlineData(SyncPhase.Channels, 100, 150, "[06:10] channels      100 / 150 read")]
    [InlineData(SyncPhase.ChannelVideos, 37, 120, "[06:10] videos        37 / 120 channels · 12,000 videos")]
    [InlineData(SyncPhase.MediaCheck, 0, null, "[06:10] media check   45,339 files checked · 3 missing")]
    [InlineData(SyncPhase.ArtRetry, 5, 20, "[06:10] art retry     5 / 20 images retried")]
    [InlineData(SyncPhase.Indexing, 120, 400, "[06:10] indexing      120 / 400 written to the new indices")]
    [InlineData(SyncPhase.IndexSwitch, 0, null, "[06:10] index switch  switching to the new indices")]
    public void Each_phase_has_its_line(SyncPhase phase, int done, int? total, string expected)
    {
        SyncProgress progress = Progress(phase, done, total, new SyncCounts { VideosRead = 12_000 }) with
        {
            FilesChecked = 45_339,
            FilesMissing = 3,
        };

        Assert.Equal(expected, SyncProgressText.Line(progress, TimeSpan.FromSeconds(370)));
    }

    [Fact]
    public void The_art_made_so_far_follows_once_there_is_some()
    {
        SyncCounts counts = new() { VideosRead = 12_000, ArtCopied = 2_300, ArtResized = 6_100 };

        Assert.Equal(
            "[06:10] videos        37 / 120 channels · 12,000 videos · art 2,300 copied, 6,100 resized",
            SyncProgressText.Line(Progress(SyncPhase.ChannelVideos, 37, 120, counts), TimeSpan.FromSeconds(370)));
    }

    // The phases whose end isn't known never show one, whatever is passed (TubeArchivist's capped total_hits).
    [Theory]
    [InlineData(SyncPhase.MediaCheck)]
    public void Phases_without_a_known_end_never_show_a_total(SyncPhase phase)
    {
        string line = SyncProgressText.Line(Progress(phase, 12, 10_000), TimeSpan.Zero);

        Assert.DoesNotContain(" / ", line, StringComparison.Ordinal);
    }

    [Fact]
    public void Every_phase_has_a_label_that_fits_the_column()
    {
        foreach (SyncPhase phase in Enum.GetValues<SyncPhase>())
        {
            string label = SyncProgressText.Label(phase);
            Assert.InRange(label.Length, 1, SyncProgressText.LabelWidth);
            Assert.NotEqual(phase.ToString(), label);
        }
    }

    private static SyncProgress Progress(SyncPhase phase, int done, int? total, SyncCounts? counts = null) =>
        new(phase, Start, done, total, counts ?? new SyncCounts(), 0, 0);
}
