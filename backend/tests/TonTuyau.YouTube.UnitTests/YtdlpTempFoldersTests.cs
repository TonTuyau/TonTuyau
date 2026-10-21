using Microsoft.Extensions.Logging.Abstractions;
using TonTuyau.Application.Library;
using TonTuyau.Application.YouTube;

namespace TonTuyau.YouTube.UnitTests;

/// <summary>
/// What yt-dlp unpacks at each start (alpha.6): a run stopped left it, 85 MB a time, until the disk was full. A fake
/// yt-dlp (Fixtures/fake-yt-dlp-unpacking.sh) unpacks as PyInstaller does: into its TMPDIR, removed only when it ends
/// by itself.
/// </summary>
public sealed class YtdlpTempFoldersTests : IDisposable
{
    private readonly string _temp = Directory.CreateTempSubdirectory("tontuyau-temp-tests-").FullName;

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private string Root => Path.Combine(_temp, "tontuyau-ytdlp");

    [Fact]
    public async Task A_listing_stopped_early_leaves_nothing_yt_dlp_unpacked()
    {
        YtdlpTempFolders folders = new(NullLogger<YtdlpTempFolders>.Instance) { Temp = _temp };
        YouTubeClient client = YouTubeClientTests.Client("fake-yt-dlp-unpacking.sh", temp: folders);

        YouTubeLookup<IReadOnlyList<YouTubeListed>> lookup = await client.ListTabAsync(
            "UCmw5b3sYz6v1sFkwM1HLs1g",
            ChannelTab.Videos,
            entry => entry.Id == "k3Fq8xZ0aQw",
            Token);

        Assert.Equal(["dQw4w9WgXcQ", "k3Fq8xZ0aQw"], lookup.Item!.Select(e => e.Id));
        string written = await File.ReadAllTextAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "unpacked.path"),
            Token);
        string unpacked = written.Trim();
        Assert.StartsWith(Root, unpacked, StringComparison.Ordinal);
        Assert.False(Directory.Exists(unpacked), unpacked);
        Assert.Empty(Directory.EnumerateFileSystemEntries(Root));
    }

    [Fact]
    public void The_sweep_removes_what_runs_left_before_the_start_and_nothing_else()
    {
        DateTime started = DateTime.UtcNow.AddMinutes(-5);
        string before = Folder("_MEI1234", started.AddHours(-1));
        string ours = Folder(Path.Combine("tontuyau-ytdlp", "0a1b"), started.AddHours(-1));
        string going = Folder("_MEI5678", started.AddMinutes(1));
        string other = Folder("someone-else", started.AddHours(-1));
        YtdlpTempFolders folders = new(NullLogger<YtdlpTempFolders>.Instance) { Temp = _temp, Started = started };

        Assert.Equal(2, folders.Sweep());

        Assert.False(Directory.Exists(before));
        Assert.False(Directory.Exists(ours));
        Assert.True(Directory.Exists(going));
        Assert.True(Directory.Exists(other));
    }

    public void Dispose() => Directory.Delete(_temp, recursive: true);

    private string Folder(string name, DateTime written)
    {
        string folder = Path.Combine(_temp, name);
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, "python.so"), "unpacked");
        Directory.SetLastWriteTimeUtc(folder, written);
        return folder;
    }
}
