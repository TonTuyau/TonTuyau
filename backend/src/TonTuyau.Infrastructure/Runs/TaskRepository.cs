using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TonTuyau.Application.Paging;
using TonTuyau.Application.Sync;
using TonTuyau.Application.Tasks;
using TonTuyau.Infrastructure.Accounts;

namespace TonTuyau.Infrastructure.Runs;

/// <summary>
/// The reads of the run history (2.0: <c>runs</c> in PostgreSQL), for the Tasks page and the dashboard: latest first,
/// by start then id (two runs can start within the same second), paged after the previous page's last run.
/// </summary>
internal sealed class TaskRepository(IServiceScopeFactory scopes) : ITaskRepository
{

    public async Task<SyncRun?> GetLatestRunAsync(SyncMode mode, CancellationToken cancellationToken)
    {
        string? json = await WithContextAsync(db => LatestFirst(db.Runs.Where(r => r.Mode == mode))
            .Select(r => r.Run)
            .FirstOrDefaultAsync(cancellationToken))
            .ConfigureAwait(false);
        return json is null ? null : RunJson.Read(json);
    }

    public Task<ResultPage<SyncRun>> GetRunsAsync(
        SyncMode? mode,
        SyncRun? before,
        int count,
        CancellationToken cancellationToken) =>
        WithContextAsync(async db =>
        {
            IQueryable<RunRow> ofMode = mode is null ? db.Runs : db.Runs.Where(r => r.Mode == mode.Value);
            long total = await ofMode
                .LongCountAsync(cancellationToken)
                .ConfigureAwait(false);
            IQueryable<RunRow> after = before is null
                ? ofMode
                : ofMode.Where(r => r.StartedAt < before.StartedAt
                    || (r.StartedAt == before.StartedAt && r.Id.CompareTo(before.Id) < 0));
            // One more than asked: whether a next page exists.
            List<string> found = await LatestFirst(after)
                .Select(r => r.Run)
                .Take(count + 1)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);
            SyncRun[] page = [.. found
                .Take(count)
                .Select(RunJson.Read)];
            string? next = found.Count > count ? page[^1].Id : null;
            return new ResultPage<SyncRun>(page, next, total);
        });

    public async Task<IReadOnlyList<SyncRun>> GetOnHoldRunsAsync(CancellationToken cancellationToken)
    {
        // A handful at most (one refresh at a time is put on hold, and waits to be resumed or canceled).
        List<string> found = await WithContextAsync(db => db.Runs
            .Where(r => r.Outcome == SyncOutcome.OnHold)
            .Select(r => r.Run)
            .ToListAsync(cancellationToken))
            .ConfigureAwait(false);
        return [.. found
            .Select(RunJson.Read)
            .OrderBy(r => r.ResumeAt)];
    }

    public async Task<IReadOnlyList<SyncRun>> GetQueuedRunsAsync(CancellationToken cancellationToken)
    {
        // While it's queued, a run's start is when it was asked for: the queue's order, ties by id.
        List<string> found = await WithContextAsync(db => db.Runs
            .Where(r => r.Outcome == SyncOutcome.Queued)
            .OrderBy(r => r.StartedAt)
            .ThenBy(r => r.Id)
            .Select(r => r.Run)
            .ToListAsync(cancellationToken))
            .ConfigureAwait(false);
        return [.. found.Select(RunJson.Read)];
    }

    public async Task<IReadOnlyList<SyncRun>> GetPendingRunsAsync(SyncMode mode, CancellationToken cancellationToken)
    {
        // A handful at most: what waits, the one going on, the ones on hold.
        List<string> found = await WithContextAsync(db => db.Runs
            .Where(r => r.Mode == mode
                && (r.Outcome == SyncOutcome.Queued
                    || r.Outcome == SyncOutcome.Running
                    || r.Outcome == SyncOutcome.OnHold))
            .OrderBy(r => r.StartedAt)
            .ThenBy(r => r.Id)
            .Select(r => r.Run)
            .ToListAsync(cancellationToken))
            .ConfigureAwait(false);
        return [.. found.Select(RunJson.Read)];
    }

    /// <returns>The latest TubeArchivist import that succeeded, or null when none did.</returns>
    /// <summary>
    /// The runs that can add videos to the library (2.0, alpha.8): TubeArchivist's import and TonTuyau's downloads (a
    /// video, the YouTube syncs, a channel's failed downloads tried again). Not an index rebuild nor a refresh.
    /// </summary>
    private static readonly SyncMode[] AddingModes =
    [
        SyncMode.Import,
        SyncMode.DownloadVideo,
        SyncMode.YouTubeSync,
        SyncMode.YouTubeFullSync,
        SyncMode.RetryDownloads,
    ];

    /// <returns>The latest finished run that added videos to the library (an import or a download); null when none.</returns>
    public async Task<SyncRun?> GetLatestAdditionAsync(CancellationToken cancellationToken)
    {
        string? json = await WithContextAsync(db => db.Runs
            .Where(r => AddingModes.Contains(r.Mode) && r.FinishedAt != null && r.Created > 0)
            .OrderByDescending(r => r.FinishedAt)
            .ThenByDescending(r => r.Id)
            .Select(r => r.Run)
            .FirstOrDefaultAsync(cancellationToken))
            .ConfigureAwait(false);
        return json is null ? null : RunJson.Read(json);
    }

    /// <param name="since">Null for every run.</param>
    /// <returns>
    /// The runs that can add videos to the library (<see cref="AddingModes"/>) started from <paramref name="since"/>
    /// on, oldest first.
    /// </returns>
    public async Task<IReadOnlyList<SyncRun>> GetAdditionsSinceAsync(
        DateTimeOffset? since,
        CancellationToken cancellationToken)
    {
        // PostgreSQL takes instants in UTC only (the dashboard's are in its time zone).
        DateTimeOffset? from = since?.ToUniversalTime();
        List<string> found = await WithContextAsync(db => db.Runs
            .Where(r => AddingModes.Contains(r.Mode) && (from == null || r.StartedAt >= from))
            .OrderBy(r => r.StartedAt)
            .ThenBy(r => r.Id)
            .Select(r => r.Run)
            .ToListAsync(cancellationToken))
            .ConfigureAwait(false);
        return [.. found.Select(RunJson.Read)];
    }

    private static IQueryable<RunRow> LatestFirst(IQueryable<RunRow> runs) =>
        runs
            .OrderByDescending(r => r.StartedAt)
            .ThenByDescending(r => r.Id);

    private async Task<T> WithContextAsync<T>(Func<TonTuyauDbContext, Task<T>> work)
    {
        AsyncServiceScope scope = scopes.CreateAsyncScope();
        await using (scope.ConfigureAwait(false))
        {
            return await work(scope.ServiceProvider.GetRequiredService<TonTuyauDbContext>()).ConfigureAwait(false);
        }
    }
}
