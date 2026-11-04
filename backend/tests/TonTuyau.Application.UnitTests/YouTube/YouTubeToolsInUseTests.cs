using TonTuyau.Application.Library;
using TonTuyau.Application.YouTube;

namespace TonTuyau.Application.UnitTests.YouTube;

public sealed class YouTubeToolsInUseTests
{
    private const string Bundled = "/opt/yt-dlp/yt-dlp";
    private const string Downloaded = "/data/tools/yt-dlp/2026.10.10/yt-dlp";

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task The_versions_are_read_once_then_kept()
    {
        CountingYouTubeClient youTube = new();
        YouTubeToolsInUse tools = new(youTube, new YtdlpLocator(Bundled));

        Assert.Equal(new YouTubeTools("2026.08.19", "2.9.7"), await tools.GetAsync(Token));
        Assert.Equal(new YouTubeTools("2026.08.19", "2.9.7"), await tools.GetAsync(Token));
        Assert.Equal(1, youTube.ToolsAsked);
    }

    [Fact]
    public async Task Read_again_after_a_switch_the_new_versions_are_kept()
    {
        CountingYouTubeClient youTube = new();
        YtdlpLocator locator = new(Bundled);
        YouTubeToolsInUse tools = new(youTube, locator);
        await tools.ReadAsync(Token);

        locator.Switch(Downloaded, "2026.10.10");
        youTube.Tools = new YouTubeTools("2026.10.10", "2.9.7");
        await tools.ReadAsync(Token);

        Assert.Equal(new YouTubeTools("2026.10.10", "2.9.7"), await tools.GetAsync(Token));
        Assert.Equal(2, youTube.ToolsAsked);
    }

    [Fact]
    public async Task Right_after_a_switch_the_versions_are_the_new_yt_dlp_s_before_the_updater_reads_them()
    {
        CountingYouTubeClient youTube = new();
        YtdlpLocator locator = new(Bundled);
        YouTubeToolsInUse tools = new(youTube, locator);
        await tools.GetAsync(Token);

        locator.Switch(Downloaded, "2026.10.10");
        youTube.Tools = new YouTubeTools("2026.10.10", "2.9.7");

        Assert.Equal(new YouTubeTools("2026.10.10", "2.9.7"), await tools.GetAsync(Token));
        Assert.Equal(new YouTubeTools("2026.10.10", "2.9.7"), await tools.GetAsync(Token));
        Assert.Equal(2, youTube.ToolsAsked);
    }

    [Fact]
    public async Task Versions_read_while_yt_dlp_switched_are_read_again()
    {
        YtdlpLocator locator = new(Bundled);
        SwitchingYouTubeClient youTube = new(() => locator.Switch(Downloaded, "2026.10.10"));
        YouTubeToolsInUse tools = new(youTube, locator);

        await tools.ReadAsync(Token);
        youTube.SwitchMeanwhile = null;
        await tools.GetAsync(Token);

        Assert.Equal(2, youTube.ToolsAsked);
    }

    /// <summary>A yt-dlp switched while its versions are read (the updater's switch, meanwhile).</summary>
    private sealed class SwitchingYouTubeClient(Action? switchMeanwhile) : IYouTubeClient
    {
        public Action? SwitchMeanwhile { get; set; } = switchMeanwhile;

        public int ToolsAsked { get; private set; }

        public Task<YouTubeTools> GetToolsAsync(CancellationToken cancellationToken)
        {
            ToolsAsked++;
            SwitchMeanwhile?.Invoke();
            return Task.FromResult(new YouTubeTools("2026.10.10", "2.9.7"));
        }

        public Task<YouTubeLookup<YouTubeVideo>> GetVideoAsync(
            string videoId,
            YouTubeCommentsRequest? comments,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<YouTubeLookup<YouTubeChannel>> GetChannelAsync(
            string channelId,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<YouTubeLookup<YouTubeDownload>> DownloadVideoAsync(
            string videoId,
            string format,
            string folder,
            bool retryWithoutCookie,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<YouTubeCookieCheck> CheckCookieAsync(CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<YouTubeLookup<IReadOnlyList<YouTubeListed>>> ListTabAsync(
            string channelId,
            ChannelTab tab,
            Func<YouTubeListed, bool> stopAt,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }
}
