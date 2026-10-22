using TonTuyau.Infrastructure.Streaming;

namespace TonTuyau.Infrastructure.UnitTests.Streaming;

/// <summary>
/// A file's keyframes from its own index (C3). The fixtures are 10 s with a keyframe every 2 s (ffprobe says so):
/// H.264 with B-frames in MP4 (TubeArchivist's), its index first or last, VP9 and Opus in Matroska (TonTuyau's).
/// </summary>
public sealed class KeyframeIndexTests
{
    private static readonly double[] EveryTwoSeconds = [0, 2, 4, 6, 8];

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData("h264-front.mp4")]
    [InlineData("h264-end.mp4")]
    [InlineData("vp9.mkv")]
    public async Task The_keyframes_are_read_from_the_index(string name)
    {
        IReadOnlyList<double>? times = await new FileKeyframeReader().ReadAsync(Fixture(name), Token);

        Assert.NotNull(times);
        Assert.Equal(EveryTwoSeconds, times.Select(t => Math.Round(t, 3)));
    }

    [Fact]
    public async Task A_fragmented_mp4_has_no_index_to_read()
    {
        Assert.Null(await new FileKeyframeReader().ReadAsync(Fixture("fragmented.mp4"), Token));
    }

    [Fact]
    public async Task Anything_else_has_none_either()
    {
        string file = Path.Combine(Path.GetTempPath(), $"not-a-video-{Guid.NewGuid():N}.txt");
        await File.WriteAllTextAsync(file, "Not a video at all, but long enough.", Token);
        try
        {
            Assert.Null(await new FileKeyframeReader().ReadAsync(file, Token));
        }
        finally
        {
            File.Delete(file);
        }
    }

    private static string Fixture(string name) => Path.Combine(AppContext.BaseDirectory, "Fixtures", "Streaming", name);
}
