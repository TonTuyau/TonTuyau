using TonTuyau.Application.Browse;
using TonTuyau.Application.Configuration;
using TonTuyau.Application.Library;
using TonTuyau.Application.Paging;
using TonTuyau.Application.RunLogs;
using TonTuyau.Application.Sync;
using TonTuyau.Application.Validation;

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
        return new TasksView(holder, runningHere, [incremental, full, deletions]);
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
            [.. page.Items.Select(r => new RunView(r, r.Id == runningHere))],
            page.NextCursor,
            page.Total);
    }

    /// <summary>Starts a task now, in this backend's background: the run, just started.</summary>
    /// <param name="account">Who starts it: the run's <see cref="SyncRun.StartedBy"/>.</param>
    /// <exception cref="ValidationException">Not a task.</exception>
    /// <exception cref="ConflictException">A run is going on, or no deletion list waits to be applied.</exception>
    public async Task<RunView> StartAsync(string task, string account, CancellationToken cancellationToken)
    {
        if (!ApiValues.Tasks.TryGet(task, out SyncMode mode))
        {
            throw RequestRules.Invalid("task", $"must be one of {ApiValues.Tasks.Choices}.");
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
            started = await runner.StartAsync(mode, SyncTrigger.Admin, appliesTo, account).ConfigureAwait(false);
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

    /// <summary>Asks the run going on to stop: it ends canceled soon after (between two requests).</summary>
    /// <param name="account">Who asks: the run's log says it.</param>
    /// <exception cref="NotFoundException">No such run.</exception>
    /// <exception cref="ConflictException">It's over, or it goes on in another process.</exception>
    public async Task<RunView> CancelAsync(string id, string account, CancellationToken cancellationToken)
    {
        SyncRun run = await FindAsync(id, cancellationToken).ConfigureAwait(false);
        if (runner.Cancel(run.Id, account))
        {
            return new RunView(run, Cancellable: true);
        }

        throw new ConflictException(run.Outcome == SyncOutcome.Running
            ? "This run goes on in another process (the command line): stop it there."
            : "This run is over.");
    }

    /// <exception cref="NotFoundException">No such run.</exception>
    public async Task<RunView> GetRunAsync(string id, CancellationToken cancellationToken)
    {
        SyncRun run = await FindAsync(id, cancellationToken).ConfigureAwait(false);
        return new RunView(run, runner.RunningId == run.Id);
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
        return run is null ? null : new RunView(run, run.Id == runningHere);
    }

    private async Task<SyncRun> FindAsync(string id, CancellationToken cancellationToken)
    {
        SyncRun? run = LibraryPaths.IsRunId(id)
            ? await state.GetRunAsync(id, cancellationToken).ConfigureAwait(false)
            : null;
        return run ?? throw new NotFoundException($"No run {id}.");
    }
}
