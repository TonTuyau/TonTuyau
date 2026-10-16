using TonTuyau.Application.Browse;
using TonTuyau.Application.Configuration;
using TonTuyau.Application.Library;
using TonTuyau.Application.Paging;
using TonTuyau.Application.RunLogs;
using TonTuyau.Application.Sync;
using TonTuyau.Application.Validation;
using TonTuyau.Application.YouTube;

namespace TonTuyau.Application.Tasks;

/// <summary>
/// The Tasks page's reads (1.4): the three tasks (incremental sync, full sync, applying deletions), the run history,
/// a run, its log. The run history is in Elasticsearch (<see cref="ITaskRepository"/>, <see cref="ISyncStateStore"/>),
/// the logs in PostgreSQL (<see cref="IRunLogRepository"/>).
/// </summary>
public sealed class TaskService(
    ITaskRepository runs,
    ISyncStateStore state,
    IRunLogRepository logs,
    SyncRunner runner,
    YouTubeRefreshService refresh,
    SyncSchedule schedule,
    IValidator<RunListRequest> runListValidator,
    IValidator<RunLogRequest> runLogValidator,
    TonTuyauOptions options,
    TimeProvider clock)
{
    public const int DefaultRunsPerPage = 20;
    public const int MaxRunsPerPage = 100;
    public const int DefaultLogLinesPerPage = 500;
    public const int MaxLogLinesPerPage = 2000;

    /// <summary>
    /// How long after a run's end its log is all written (the writer writes a batch a second): the command line waits
    /// that long at the end of each run.
    /// </summary>
    public static readonly TimeSpan LogSettles = TimeSpan.FromSeconds(2);

    public async Task<TasksView> GetTasksAsync(CancellationToken cancellationToken)
    {
        SyncLockInfo? holder = await state
            .GetLockAsync(cancellationToken)
            .ConfigureAwait(false);
        string? runningHere = runner.RunningId;
        DateTimeOffset now = clock.GetUtcNow();
        SyncOptions sync = options.Sync;
        TaskView incremental = new(
            SyncMode.Incremental,
            sync.IncrementalCron,
            sync.SchedulerEnabled ? schedule.NextOf(SyncMode.Incremental, now) : null,
            await LatestAsync(SyncMode.Incremental, runningHere, cancellationToken).ConfigureAwait(false),
            null);
        TaskView full = new(
            SyncMode.Full,
            sync.FullCron,
            sync.SchedulerEnabled ? schedule.NextOf(SyncMode.Full, now) : null,
            await LatestAsync(SyncMode.Full, runningHere, cancellationToken).ConfigureAwait(false),
            null);
        TaskView deletions = new(
            SyncMode.ApplyDeletions,
            null,
            null,
            await LatestAsync(SyncMode.ApplyDeletions, runningHere, cancellationToken).ConfigureAwait(false),
            await WaitingAsync(cancellationToken).ConfigureAwait(false));
        IReadOnlyList<SyncRun> onHold = await runs.GetOnHoldRunsAsync(cancellationToken).ConfigureAwait(false);
        return new TasksView(
            holder,
            runningHere,
            [incremental, full, deletions],
            [.. onHold.Select(r => new RunView(r, Cancellable: true))]);
    }

    /// <exception cref="ValidationException">The request isn't valid.</exception>
    public async Task<ResultPage<RunView>> GetRunsAsync(RunListRequest request, CancellationToken cancellationToken)
    {
        runListValidator.Validate(request);
        SyncMode? mode = request.Task is null ? null : ApiValues.Tasks[request.Task];
        SyncRun? before = null;
        if (request.Before is not null)
        {
            before = await state.GetRunAsync(request.Before, cancellationToken).ConfigureAwait(false)
                ?? throw RequestRules.Invalid("before", "isn't a run of the history.");
        }

        ResultPage<SyncRun> page = await runs
            .GetRunsAsync(mode, before, request.Size ?? DefaultRunsPerPage, cancellationToken)
            .ConfigureAwait(false);
        string? runningHere = runner.RunningId;
        return new ResultPage<RunView>(
            [.. page.Items.Select(r => View(r, runningHere))],
            page.NextCursor,
            page.Total);
    }

    /// <summary>Starts a task now, in this backend's background: the run, just started.</summary>
    /// <param name="target">A refresh's video or channel (its YouTube id, 1.5); null for the other tasks.</param>
    /// <param name="account">Who starts it: the run's <see cref="SyncRun.StartedBy"/>.</param>
    /// <exception cref="ValidationException">Not a task, or a target missing or not expected.</exception>
    /// <exception cref="ConflictException">
    /// A run is going on, no deletion list waits to be applied, or the target isn't one of the library.
    /// </exception>
    public async Task<RunView> StartAsync(
        string task,
        string? target,
        string account,
        CancellationToken cancellationToken)
    {
        if (!ApiValues.Tasks.TryGet(task, out SyncMode mode))
        {
            throw RequestRules.Invalid("task", $"must be one of {ApiValues.Tasks.Choices}.");
        }

        bool refresh = mode is SyncMode.RefreshVideo or SyncMode.RefreshChannel or SyncMode.RefreshChannelVideos;
        if (refresh && target is null)
        {
            throw RequestRules.Invalid("target", "is required: the video's or channel's id.");
        }

        if (!refresh && target is not null)
        {
            throw RequestRules.Invalid("target", "is only for the refreshes.");
        }

        string? appliesTo = null;
        if (mode == SyncMode.ApplyDeletions)
        {
            SyncRun waiting = await WaitingAsync(cancellationToken).ConfigureAwait(false)
                ?? throw new ConflictException("No deletion list waits to be applied.");
            appliesTo = waiting.Id;
        }

        StartedRun started;
        try
        {
            started = refresh
                ? await runner.StartRefreshAsync(mode, target!, SyncTrigger.Admin, account).ConfigureAwait(false)
                : await runner.StartAsync(mode, SyncTrigger.Admin, appliesTo, account).ConfigureAwait(false);
        }
        catch (SyncBusyException ex)
        {
            throw new ConflictException(ex.Message, ex);
        }
        catch (SyncRefusedException ex)
        {
            throw new ConflictException(ex.Message, ex);
        }

        return await GetRunAsync(started.Id, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Asks the run going on to stop: it ends canceled soon after (between two requests). A refresh on hold (1.5) is
    /// canceled at once: it won't resume.
    /// </summary>
    /// <param name="account">Who asks: the run's log says it.</param>
    /// <exception cref="NotFoundException">No such run.</exception>
    /// <exception cref="ConflictException">It's over, or it goes on in another process.</exception>
    public async Task<RunView> CancelAsync(string id, string account, CancellationToken cancellationToken)
    {
        SyncRun run = await FindAsync(id, cancellationToken).ConfigureAwait(false);
        if (run.Outcome == SyncOutcome.OnHold)
        {
            try
            {
                return new RunView(
                    await refresh.CancelHeldAsync(run.Id, account, cancellationToken).ConfigureAwait(false),
                    Cancellable: false);
            }
            catch (SyncBusyException ex)
            {
                throw new ConflictException(ex.Message, ex);
            }
            catch (SyncRefusedException ex)
            {
                throw new ConflictException(ex.Message, ex);
            }
        }

        if (runner.Cancel(run.Id, account))
        {
            return new RunView(run, Cancellable: true);
        }

        throw new ConflictException(run.Outcome == SyncOutcome.Running
            ? "This run goes on in another process (the command line): stop it there."
            : "This run is over.");
    }

    /// <summary>Resumes a refresh on hold now (1.5), instead of at the end of its cool-down: the run, going on again.</summary>
    /// <param name="account">Who asks: the run's notes say it.</param>
    /// <exception cref="NotFoundException">No such run.</exception>
    /// <exception cref="ConflictException">It isn't on hold, or a run is going on.</exception>
    /// <summary>Puts the refresh going on here on hold by hand (1.5): it waits for Resume now.</summary>
    /// <exception cref="NotFoundException">No such run.</exception>
    /// <exception cref="ConflictException">Not a refresh, over, or going on in another process.</exception>
    public async Task<RunView> HoldAsync(string id, string account, CancellationToken cancellationToken)
    {
        SyncRun run = await FindAsync(id, cancellationToken).ConfigureAwait(false);
        if (refresh.RequestHold(run.Id, account))
        {
            return new RunView(run, Cancellable: true);
        }

        throw new ConflictException(
            run.Mode is not (SyncMode.RefreshVideo or SyncMode.RefreshChannel or SyncMode.RefreshChannelVideos)
                ? "Only a refresh on YouTube can be put on hold: a sync can be canceled."
                : run.Outcome == SyncOutcome.Running
                ? "This run goes on in another process (the command line): stop it there."
                : "This run isn't going on.");
    }

    public async Task<RunView> ResumeAsync(string id, string account, CancellationToken cancellationToken)
    {
        SyncRun run = await FindAsync(id, cancellationToken).ConfigureAwait(false);
        try
        {
            await runner.StartResumeAsync(run.Id, SyncTrigger.Admin, account).ConfigureAwait(false);
        }
        catch (SyncBusyException ex)
        {
            throw new ConflictException(ex.Message, ex);
        }
        catch (SyncRefusedException ex)
        {
            throw new ConflictException(ex.Message, ex);
        }

        return await GetRunAsync(run.Id, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>A run can be canceled while it goes on here, or while it's on hold (1.5).</summary>
    private static RunView View(SyncRun run, string? runningHere) =>
        new(run, run.Id == runningHere || run.Outcome == SyncOutcome.OnHold);

    /// <exception cref="NotFoundException">No such run.</exception>
    public async Task<RunView> GetRunAsync(string id, CancellationToken cancellationToken)
    {
        SyncRun run = await FindAsync(id, cancellationToken).ConfigureAwait(false);
        return View(run, runner.RunningId);
    }

    /// <exception cref="NotFoundException">No such run.</exception>
    /// <exception cref="ValidationException">The request isn't valid.</exception>
    public async Task<RunLogView> GetLogAsync(string id, RunLogRequest request, CancellationToken cancellationToken)
    {
        runLogValidator.Validate(request);
        SyncRun run = await FindAsync(id, cancellationToken).ConfigureAwait(false);
        IReadOnlyList<RunLogLine> lines = await logs
            .GetAsync(run.Id, request.After ?? 0, request.Size ?? DefaultLogLinesPerPage, cancellationToken)
            .ConfigureAwait(false);
        bool settled = run.FinishedAt is not null && clock.GetUtcNow() - run.FinishedAt.Value >= LogSettles;
        return new RunLogView(lines, settled && lines.Count < (request.Size ?? DefaultLogLinesPerPage));
    }

    /// <returns>
    /// The latest full run that finished, when it didn't delete because its list was over the limit, and no run has
    /// applied that list yet. A newer full run (whatever its outcome) makes an older list outdated (see
    /// <see cref="SyncService.ApplyDeletionsAsync(string, SyncTrigger, CancellationToken)"/>).
    /// </returns>
    private async Task<SyncRun?> WaitingAsync(CancellationToken cancellationToken)
    {
        SyncRun? latest = await state
            .GetLatestFullRunAsync(cancellationToken)
            .ConfigureAwait(false);
        if (latest is null || !latest.DeletionsBlockedByLimit)
        {
            return null;
        }

        return await runs.WasAppliedAsync(latest.Id, cancellationToken).ConfigureAwait(false) ? null : latest;
    }

    private async Task<RunView?> LatestAsync(SyncMode mode, string? runningHere, CancellationToken cancellationToken)
    {
        SyncRun? run = await runs
            .GetLatestRunAsync(mode, cancellationToken)
            .ConfigureAwait(false);
        return run is null ? null : View(run, runningHere);
    }

    private async Task<SyncRun> FindAsync(string id, CancellationToken cancellationToken)
    {
        SyncRun? run = LibraryPaths.IsRunId(id)
            ? await state.GetRunAsync(id, cancellationToken).ConfigureAwait(false)
            : null;
        return run ?? throw new NotFoundException($"No run {id}.");
    }
}
