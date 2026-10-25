using TonTuyau.Application.Sync;
using TonTuyau.Application.YouTube;

namespace TonTuyau.Application.UnitTests.Dashboard;

/// <summary>The videos folder's disk as a test sets it (2.2.0); null: it doesn't say.</summary>
internal sealed class FakeDiskSpace(DiskSpace? space = null) : IDiskSpace
{
    public DiskSpace? Space { get; set; } = space;

    public DiskSpace? Read() => Space;
}

/// <summary>The YouTube settings, kept in memory.</summary>
internal sealed class FakeYouTubeSettings(YouTubeSettings? settings = null) : IYouTubeSettingsRepository
{
    public YouTubeSettings Settings { get; set; } = settings ?? YouTubeSettings.Default;

    public Task<YouTubeSettings> GetAsync(CancellationToken cancellationToken) => Task.FromResult(Settings);

    public Task SaveAsync(YouTubeSettings settings, CancellationToken cancellationToken)
    {
        Settings = settings;
        return Task.CompletedTask;
    }
}
