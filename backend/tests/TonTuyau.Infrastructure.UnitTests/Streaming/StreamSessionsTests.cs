using Microsoft.Extensions.Logging.Abstractions;
using TonTuyau.Application.Streaming;
using TonTuyau.Infrastructure.Streaming;

namespace TonTuyau.Infrastructure.UnitTests.Streaming;

/// <summary>
/// The parts ffmpeg copies out of a file (C3), with the real ffmpeg (skipped where there's none). The fixtures have a
/// keyframe every 2 s: here, one part each, so that seeking can be tried on 10 s.
/// </summary>
public sealed class StreamSessionsTests : IDisposable
{
    private const string Viewing = "viewing-0123456789";

    private readonly FfmpegStreamSessions _sessions = new(
        "ffmpeg",
        TimeProvider.System,
        NullLogger<FfmpegStreamSessions>.Instance);

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData("vp9.mkv")]
    [InlineData("h264-end.mp4")]
    public async Task The_parts_come_in_order_each_from_its_keyframe_at_the_file_s_own_times(string name)
    {
        StreamSource source = Source(name);

        byte[] init = await _sessions.InitAsync(source, Viewing, Token);
        List<double?> starts = [];
        foreach (StreamPart part in source.Parts)
        {
            byte[] bytes = await _sessions.PartAsync(source, Viewing, part.Index, Token);
            starts.Add(await FirstVideoTimeAsync(init, bytes));
        }

        Assert.Equal([0, 2, 4, 6, 8], starts.Select(t => Math.Round(t ?? -1, 1)));
    }

    [Theory]
    [InlineData("vp9.mkv")]
    [InlineData("h264-end.mp4")]
    public async Task A_seek_forward_or_back_copies_from_there(string name)
    {
        StreamSource source = Source(name);
        byte[] init = await _sessions.InitAsync(source, Viewing, Token);

        double? forward = await FirstVideoTimeAsync(init, await _sessions.PartAsync(source, Viewing, 4, Token));
        double? back = await FirstVideoTimeAsync(init, await _sessions.PartAsync(source, Viewing, 1, Token));

        Assert.Equal(8, Math.Round(forward ?? -1, 1));
        Assert.Equal(2, Math.Round(back ?? -1, 1));
    }

    [Fact]
    public async Task The_init_segment_is_the_same_whatever_the_part_a_viewing_starts_at()
    {
        StreamSource source = Source("vp9.mkv");
        await _sessions.PartAsync(source, "viewing-from-part-3", 3, Token);

        byte[] fromThree = await _sessions.InitAsync(source with { FileSize = -1 }, "viewing-from-part-3", Token);
        byte[] fromStart = await _sessions.InitAsync(source with { FileSize = -2 }, "viewing-from-start", Token);

        Assert.Equal(fromStart, fromThree);
    }

    [Fact]
    public async Task A_file_ffmpeg_can_t_copy_into_parts_fails_as_such()
    {
        RequireFfmpeg();
        string file = Path.Combine(Path.GetTempPath(), $"not-a-video-{Guid.NewGuid():N}.mkv");
        await File.WriteAllTextAsync(file, "Not a video at all.", Token);
        try
        {
            StreamSource source = new("notavideo00", file, 19, [new StreamPart(0, 0, 10)]);

            await Assert.ThrowsAsync<StreamFailedException>(() => _sessions.InitAsync(source, Viewing, Token));
        }
        finally
        {
            File.Delete(file);
        }
    }

    public void Dispose() => _sessions.Dispose();

    /// <summary>The fixture, a part per keyframe (0, 2, 4, 6, 8 s; the video lasts 10 s).</summary>
    private static StreamSource Source(string name)
    {
        RequireFfmpeg();
        string path = Path.Combine(AppContext.BaseDirectory, "Fixtures", "Streaming", name);
        return new StreamSource(
            name.Replace('.', '-'),
            path,
            new FileInfo(path).Length,
            [.. Enumerable.Range(0, 5).Select(i => new StreamPart(i, i * 2, 2))]);
    }

    /// <summary>When a part's first video fragment starts: read after the init segment, as a player does.</summary>
    private static async Task<double?> FirstVideoTimeAsync(byte[] init, byte[] part)
    {
        using MemoryStream stream = new([.. init, .. part]);
        FragmentedMp4Reader reader = new(stream);
        await reader.ReadInitAsync(Token);
        return (await reader.ReadFragmentAsync(Token))?.Time;
    }

    private static void RequireFfmpeg()
    {
        bool found = (Environment.GetEnvironmentVariable("PATH") ?? "")
            .Split(Path.PathSeparator)
            .Any(folder => File.Exists(Path.Combine(folder, "ffmpeg")));
        Assert.SkipUnless(found, "No ffmpeg on the PATH.");
    }
}
