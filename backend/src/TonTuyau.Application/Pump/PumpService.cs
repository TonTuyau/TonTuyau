using TonTuyau.Application.Browse;
using TonTuyau.Application.Library;
using TonTuyau.Application.Store;
using TonTuyau.Application.Sync;
using TonTuyau.Application.Tasks;
using TonTuyau.Application.Validation;

namespace TonTuyau.Application.Pump;

/// <summary>
/// The browser extension's "Add to TonTuyau" (2.0, B7): where a YouTube video or channel stands, and adding it, the
/// same runs as the Tasks page's "Download a video" and "Add a channel" (the queue, the hold, the history), started by
/// the key. A channel gets the YouTube page's default tabs. Asking for one that's already waiting gives its run, not a
/// second one.
/// </summary>
public sealed class PumpService(
    SyncRunner runner,
    IVideoStore videos,
    ISubscriptionStore subscriptions,
    ITaskRepository runs)
{
    /// <exception cref="ValidationException">Not a video id.</exception>
    public async Task<PumpItem> GetVideoAsync(string id, CancellationToken cancellationToken)
    {
        RequireVideoId(id);
        StoredVideo? stored = await videos.GetStoredAsync(id, cancellationToken).ConfigureAwait(false);
        if (stored is not null && !stored.MediaMissing)
        {
            return new PumpItem(PumpItemState.InTonTuyau);
        }

        SyncRun? pending = await PendingAsync(SyncMode.DownloadVideo, id, cancellationToken).ConfigureAwait(false);
        return pending is not null ? new PumpItem(PumpItemState.Waiting, pending.Id)
            : stored is not null ? new PumpItem(PumpItemState.FileMissing)
            : new PumpItem(PumpItemState.Absent);
    }

    /// <exception cref="ValidationException">Not a channel id.</exception>
    public async Task<PumpItem> GetChannelAsync(string id, CancellationToken cancellationToken)
    {
        RequireChannelId(id);
        if (await subscriptions.GetAsync(id, cancellationToken).ConfigureAwait(false) is not null)
        {
            return new PumpItem(PumpItemState.InTonTuyau);
        }

        SyncRun? pending = await PendingAsync(SyncMode.AddChannel, id, cancellationToken).ConfigureAwait(false);
        return pending is not null
            ? new PumpItem(PumpItemState.Waiting, pending.Id)
            : new PumpItem(PumpItemState.Absent);
    }

    /// <summary>Downloads the video (through the queue); one already waiting: its run.</summary>
    /// <param name="key">The key's name, the run's <see cref="SyncRun.StartedBy"/>.</param>
    /// <exception cref="ValidationException">Not a video id.</exception>
    /// <exception cref="ConflictException">In TonTuyau already, with its file.</exception>
    public async Task<PumpItem> AddVideoAsync(string id, string key, CancellationToken cancellationToken)
    {
        PumpItem now = await GetVideoAsync(id, cancellationToken).ConfigureAwait(false);
        if (now.State == PumpItemState.Waiting)
        {
            return now;
        }

        StartedRun started = await StartAsync(
            () => runner.StartRefreshAsync(SyncMode.DownloadVideo, id, SyncTrigger.Extension, key))
            .ConfigureAwait(false);
        return new PumpItem(PumpItemState.Waiting, started.Id);
    }

    /// <summary>Subscribes to the channel with the default tabs, added when it's new (through the queue).</summary>
    /// <param name="key">The key's name, the run's <see cref="SyncRun.StartedBy"/>.</param>
    /// <exception cref="ValidationException">Not a channel id.</exception>
    /// <exception cref="ConflictException">TonTuyau is subscribed to it already.</exception>
    public async Task<PumpItem> AddChannelAsync(string id, string key, CancellationToken cancellationToken)
    {
        PumpItem now = await GetChannelAsync(id, cancellationToken).ConfigureAwait(false);
        if (now.State == PumpItemState.Waiting)
        {
            return now;
        }

        StartedRun started = await StartAsync(
            () => runner.StartAddChannelAsync(id, tabs: null, SyncTrigger.Extension, key))
            .ConfigureAwait(false);
        return new PumpItem(PumpItemState.Waiting, started.Id);
    }

    private static async Task<StartedRun> StartAsync(Func<Task<StartedRun>> start)
    {
        try
        {
            return await start().ConfigureAwait(false);
        }
        catch (SyncRefusedException ex)
        {
            throw new ConflictException(ex.Message, ex);
        }
    }

    /// <returns>The run of that kind for this target not over yet; null when there's none.</returns>
    private async Task<SyncRun?> PendingAsync(SyncMode mode, string target, CancellationToken cancellationToken)
    {
        IReadOnlyList<SyncRun> pending = await runs
            .GetPendingRunsAsync(mode, cancellationToken)
            .ConfigureAwait(false);
        return pending.FirstOrDefault(r => r.Target == target);
    }

    private static void RequireVideoId(string id)
    {
        if (!LibraryIds.IsVideoId(id))
        {
            throw RequestRules.Invalid("id", "must be a YouTube video id (11 characters).");
        }
    }

    private static void RequireChannelId(string id)
    {
        if (!LibraryIds.IsChannelId(id))
        {
            throw RequestRules.Invalid("id", "must be a YouTube channel id (UC and 22 characters).");
        }
    }
}
