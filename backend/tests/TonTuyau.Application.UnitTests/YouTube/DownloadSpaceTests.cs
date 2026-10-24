using TonTuyau.Application.Sync;
using TonTuyau.Application.UnitTests.Dashboard;
using TonTuyau.Application.YouTube;

namespace TonTuyau.Application.UnitTests.YouTube;

/// <summary>Whether the downloads may run (2.2.0): under the threshold, set to wait, they don't.</summary>
public sealed class DownloadSpaceTests
{
    private const long Gb = DownloadSpace.BytesPerGb;

    private readonly FakeDiskSpace _disk = new(new DiskSpace(1_000 * Gb, 40 * Gb));
    private readonly FakeYouTubeSettings _settings = new();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Under_the_threshold_the_downloads_wait_saying_why()
    {
        string? why = await new DownloadSpace(_disk, _settings).WhyWaitAsync(Ct);

        Assert.Equal(
            "Under 50 GB free on the videos folder's disk (40 GB): the downloads wait until space is freed "
                + "(the YouTube page sets it).",
            why);
    }

    [Fact]
    public async Task Just_under_the_threshold_says_its_free_space_rounded_down()
    {
        _disk.Space = new DiskSpace(1_000 * Gb, (50 * Gb) - 1);

        Assert.StartsWith(
            "Under 50 GB free on the videos folder's disk (49.9 GB)",
            await new DownloadSpace(_disk, _settings).WhyWaitAsync(Ct),
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task At_the_threshold_or_above_they_run()
    {
        _disk.Space = new DiskSpace(1_000 * Gb, 50 * Gb);

        Assert.Null(await new DownloadSpace(_disk, _settings).WhyWaitAsync(Ct));
    }

    [Fact]
    public async Task Set_to_go_on_at_a_threshold_of_0_or_on_a_disk_that_doesnt_say_they_run()
    {
        _settings.Settings = YouTubeSettings.Default with { HoldBelowMinFree = false };
        Assert.Null(await new DownloadSpace(_disk, _settings).WhyWaitAsync(Ct));

        _settings.Settings = YouTubeSettings.Default with { MinFreeGb = 0 };
        Assert.Null(await new DownloadSpace(_disk, _settings).WhyWaitAsync(Ct));

        _settings.Settings = YouTubeSettings.Default;
        _disk.Space = null;
        Assert.Null(await new DownloadSpace(_disk, _settings).WhyWaitAsync(Ct));
    }
}
