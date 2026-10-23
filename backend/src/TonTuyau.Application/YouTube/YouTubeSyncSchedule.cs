using TonTuyau.Application.Store;
using TonTuyau.Application.Sync;
using TonTuyau.Application.Tasks;

namespace TonTuyau.Application.YouTube;

/// <summary>
/// When the incremental YouTube sync runs by itself (2.0, B5): <see cref="YouTubeSettings.SyncEveryMinutes"/> after the
/// last one was asked for (at once when none ever ran), through the run queue, while TonTuyau is subscribed to a
/// channel. One at a time: not while one waits, runs or is on hold. The full sync is by hand only.
/// </summary>
public sealed class YouTubeSyncSchedule(
    SyncRunner runner,
    ITaskRepository runs,
    ISubscriptionStore subscriptions,
    IYouTubeSettingsRepository settings,
    TimeProvider clock)
{
    /// <returns>When it's due next; null when it's by hand only (0 minutes).</returns>
    public async Task<DateTimeOffset?> NextAsync(CancellationToken cancellationToken)
    {
        YouTubeSettings read = await settings.GetAsync(cancellationToken).ConfigureAwait(false);
        SyncRun? last = await runs.GetLatestRunAsync(SyncMode.YouTubeSync, cancellationToken).ConfigureAwait(false);
        return Next(read, last);
    }

    /// <summary>Asks for the incremental sync when it's due.</summary>
    /// <returns>The run asked for; null when it isn't due, or nothing is subscribed to.</returns>
    public async Task<StartedRun?> QueueIfDueAsync(CancellationToken cancellationToken)
    {
        YouTubeSettings read = await settings.GetAsync(cancellationToken).ConfigureAwait(false);
        SyncRun? last = await runs.GetLatestRunAsync(SyncMode.YouTubeSync, cancellationToken).ConfigureAwait(false);
        if (Next(read, last) is not { } due
            || due > clock.GetUtcNow()
            || last?.Outcome is SyncOutcome.Queued or SyncOutcome.Running or SyncOutcome.OnHold
            || (await subscriptions.ListAsync(cancellationToken).ConfigureAwait(false)).Count == 0)
        {
            return null;
        }

        return await runner.StartYouTubeSyncAsync(full: false, SyncTrigger.Scheduler).ConfigureAwait(false);
    }

    private DateTimeOffset? Next(YouTubeSettings read, SyncRun? last) =>
        read.SyncEveryMinutes <= 0 ? null
        : last is null ? clock.GetUtcNow()
        : (last.QueuedAt ?? last.StartedAt).AddMinutes(read.SyncEveryMinutes);
}
