using TonTuyau.Application.YouTube;

namespace TonTuyau.Application.UnitTests.YouTube;

public sealed class YouTubeToolsInUseTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task The_versions_are_read_once_then_kept()
    {
        CountingYouTubeClient youTube = new();
        YouTubeToolsInUse tools = new(youTube);

        Assert.Equal(new YouTubeTools("2026.08.19", "2.9.7"), await tools.GetAsync(Token));
        Assert.Equal(new YouTubeTools("2026.08.19", "2.9.7"), await tools.GetAsync(Token));
        Assert.Equal(1, youTube.ToolsAsked);
    }

    [Fact]
    public async Task Read_again_after_a_switch_the_new_versions_are_kept()
    {
        CountingYouTubeClient youTube = new();
        YouTubeToolsInUse tools = new(youTube);
        await tools.ReadAsync(Token);

        youTube.Tools = new YouTubeTools("2026.10.10", "2.9.7");
        await tools.ReadAsync(Token);

        Assert.Equal(new YouTubeTools("2026.10.10", "2.9.7"), await tools.GetAsync(Token));
        Assert.Equal(2, youTube.ToolsAsked);
    }
}
