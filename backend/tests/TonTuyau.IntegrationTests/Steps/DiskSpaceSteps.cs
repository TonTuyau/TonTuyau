using Microsoft.Extensions.DependencyInjection;
using TonTuyau.Application.Sync;
using TonTuyau.IntegrationTests.Support;

namespace TonTuyau.IntegrationTests.Steps;

/// <summary>The videos folder's disk the backend sees (2.2.0, <see cref="FakeDiskSpace"/>), from now on.</summary>
[Binding]
public sealed class DiskSpaceSteps(ScenarioState state)
{
    private const long Gb = FakeDiskSpace.Gb;

    [Given("the videos folder's disk has {int} GB free of {int} GB")]
    public async Task DiskHas(int free, int total) => await SetAsync(total * Gb, free * Gb);

    /// <summary>Just over a threshold: the fake yt-dlp's 4096-byte files take it under.</summary>
    [Given("the videos folder's disk has {int} GB and {int} bytes free")]
    public async Task DiskHasBytes(int free, int bytes) => await SetAsync(1000 * Gb, (free * Gb) + bytes);

    [Given("the videos folder's disk doesn't say its free space")]
    public void DiskDoesntSay() => state.Disk.Unknown();

    /// <summary>Once the backend has started: the files the media folder has by then aren't taken from it.</summary>
    private async Task SetAsync(long total, long free)
    {
        await state.WaitUntilReadyAsync();
        state.Backend.Services.GetRequiredService<IDiskSpace>();
        state.Disk.Set(total, free);
    }
}
