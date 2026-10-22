using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TonTuyau.Application.Configuration;
using TonTuyau.Application.Library;
using TonTuyau.Application.Store;
using TonTuyau.Infrastructure.Accounts;
using TonTuyau.Infrastructure.Library;
using TonTuyau.Infrastructure.Storage;
using TonTuyau.IntegrationTests.Support;

namespace TonTuyau.IntegrationTests.Steps;

/// <summary>
/// TonTuyau's own downloads (2.0), through the fake yt-dlp (<see cref="YouTubeSteps.Answers"/>): what it left in the
/// media folder, and what it was asked for.
/// </summary>
[Binding]
public sealed class DownloadSteps(ScenarioState state, YouTubeSteps youTube, AuthSteps auth)
{
    private string MediaRoot => state.Backend.Services.GetRequiredService<TonTuyauOptions>().Storage.MediaRoot;

    [Then("the media folder has the file of the video {string} with {int} bytes")]
    public void FileInPlace(string name, int bytes)
    {
        string file = Path.Combine(
            MediaRoot,
            FakeTubeArchivist.ChannelId(name.Split('-')[0]),
            FakeTubeArchivist.VideoId(name) + ".mkv");
        Assert.True(File.Exists(file), $"no {file}");
        Assert.Equal(bytes, new FileInfo(file).Length);
    }

    [Then("no download is left in the media folder")]
    public void NothingLeft()
    {
        string downloads = Path.Combine(MediaRoot, MediaLibrary.DownloadsFolder);
        Assert.True(
            !Directory.Exists(downloads) || !Directory.EnumerateFileSystemEntries(downloads).Any(),
            string.Join(", ", Directory.EnumerateFileSystemEntries(downloads)));
    }

    [Then("yt-dlp was asked for the format {string} for the video {string}")]
    public async Task FormatAsked(string format, string name) =>
        Assert.Equal(
            format,
            await File.ReadAllTextAsync(Path.Combine(youTube.Answers, FakeTubeArchivist.VideoId(name) + ".format")));

    /// <summary>The fake yt-dlp keeps the format of each video it downloads: forgotten, to see the next downloads.</summary>
    [When("yt-dlp's downloads are forgotten")]
    public void ForgetDownloads()
    {
        foreach (string asked in Directory.EnumerateFiles(youTube.Answers, "*.format"))
        {
            File.Delete(asked);
        }
    }

    [Then("yt-dlp didn't download the video {string}")]
    public void NotDownloaded(string name) =>
        Assert.False(File.Exists(Path.Combine(youTube.Answers, FakeTubeArchivist.VideoId(name) + ".format")));

    [Given("the file of the video {string} was found missing")]
    public Task FoundMissing(string name) =>
        state.Backend.Services
            .GetRequiredService<IVideoRepository>()
            .SetMediaMissingAsync([FakeTubeArchivist.VideoId(name)], true, CancellationToken.None);

    [When("I download {string} with my session")]
    public Task Download(string target) =>
        auth.PostWithSession($"/api/admin/tasks/downloadVideo/runs?target={Uri.EscapeDataString(target)}");

    [Then("the video {string} has the file TonTuyau downloaded, and is still TubeArchivist's")]
    public async Task DownloadedFile(string name)
    {
        await using AsyncServiceScope scope = state.Backend.Services.CreateAsyncScope();
        TonTuyauDbContext db = scope.ServiceProvider.GetRequiredService<TonTuyauDbContext>();
        VideoRow row = await db.Videos.SingleAsync(v => v.Id == FakeTubeArchivist.VideoId(name));
        Assert.Equal($"{row.ChannelId}/{row.Id}.mkv", row.MediaPath);
        Assert.Equal(4096, row.MediaSize);
        Assert.False(row.MediaMissing);
        Assert.Equal(LibraryOrigin.TubeArchivist, row.Origin);
    }

    [Then("the video {string} and its channel {string} are TonTuyau's own")]
    public async Task TonTuyausOwn(string video, string channel)
    {
        await using AsyncServiceScope scope = state.Backend.Services.CreateAsyncScope();
        TonTuyauDbContext db = scope.ServiceProvider.GetRequiredService<TonTuyauDbContext>();
        VideoRow row = await db.Videos.SingleAsync(v => v.Id == FakeTubeArchivist.VideoId(video));
        Assert.Equal(LibraryOrigin.TonTuyau, row.Origin);
        Assert.Equal(LibraryOrigin.TonTuyau, row.Channel.Origin);
        Assert.Equal(FakeTubeArchivist.ChannelId(channel), row.ChannelId);
    }
}
