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
/// The Tasks page's reads (1.4): its task (2.0: the TubeArchivist import), the run history, a run, its log, all in
/// PostgreSQL (<see cref="ITaskRepository"/>, <see cref="ISyncStateStore"/>, <see cref="IRunLogRepository"/>).
/// </summary>
public sealed class TaskService(
    ITaskRepository runs,
    ISyncStateStore state,
    IRunLogRepository logs,
    SyncRunner runner,
    SyncService sync,
    YouTubeRefreshService refresh,
    IValidator<RunListRequest> runListValidator,
    IValidator<RunLogRequest> runLogValidator,
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
        TaskView import = new(
            SyncMode.Import,
            await LatestAsync(SyncMode.Import, runningHere, cancellationToken).ConfigureAwait(false),
            sync.CanImport ? null : SyncService.NoTubeArchivist);
        IReadOnlyList<SyncRun> onHold = await runs.GetOnHoldRunsAsync(cancellationToken).ConfigureAwait(false);
        IReadOnlyList<SyncRun> queue = await runs.GetQueuedRunsAsync(cancellationToken).ConfigureAwait(false);
        return new TasksView(
            holder,
            runningHere,
            [import],
            [.. onHold.Select(r => new RunView(r, Cancellable: true))],
            [.. queue.Select((r, index) => new RunView(r, Cancellable: true, index + 1))]);
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

    /// <summary>
    /// Asks for a task (2.0: through the run queue): the run, started at once when nothing else goes on, else queued.
    /// </summary>
    /// <param name="target">
    /// A refresh's video or channel (its YouTube id, 1.5), a download's video (its id or a YouTube link to it, 2.0);
    /// null for the other tasks.
    /// </param>
    /// <param name="account">Who starts it: the run's <see cref="SyncRun.StartedBy"/>.</param>
    /// <exception cref="ValidationException">Not a task, or a target missing or not expected.</exception>
    /// <exception cref="ConflictException">
    /// TubeArchivist isn't set up (the import), the target isn't one of the library (a refresh), or is already, with its
    /// file (a download).
    /// </exception>
    public async Task<RunView> StartAsync(
        string task,
        string? target,
        string account,
        CancellationToken cancellationToken)
    {
        // An index rebuild starts from the command line only.
        if (!ApiValues.Tasks.TryGet(task, out SyncMode mode) || mode is SyncMode.IndexRebuild)
        {
            throw RequestRules.Invalid(
                "task",
                "must be import, refreshVideo, refreshChannel, refreshChannelVideos or downloadVideo.");
        }

        bool refresh = mode is SyncMode.RefreshVideo
            or SyncMode.RefreshChannel
            or SyncMode.RefreshChannelVideos
            or SyncMode.DownloadVideo;
        if (refresh && target is null)
        {
            throw RequestRules.Invalid("target", "is required: the video's or channel's id.");
        }

        if (!refresh && target is not null)
        {
            throw RequestRules.Invalid("target", "is only for the refreshes and the downloads.");
        }

        if (mode == SyncMode.DownloadVideo)
        {
            target = LibraryIds.VideoIdOf(target)
                ?? throw RequestRules.Invalid("target", "must be a YouTube video's id or a link to it.");
        }

        StartedRun started;
        try
        {
            started = refresh
                ? await runner.StartRefreshAsync(mode, target!, SyncTrigger.Admin, account).ConfigureAwait(false)
                : await runner.StartImportAsync(SyncTrigger.Admin, account).ConfigureAwait(false);
        }
        catch (SyncRefusedException ex)
        {
            throw new ConflictException(ex.Message, ex);
        }

        return await GetRunAsync(started.Id, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Asks the run going on to stop: it ends canceled soon after (between two requests). A refresh on hold (1.5) is
    /// canceled at once: it won't resume, and the queue goes on. A run waiting in the queue (2.0) won't start.
    /// </summary>
    /// <param name="account">Who asks: the run's log says it.</param>
    /// <exception cref="NotFoundException">No such run.</exception>
    /// <exception cref="ConflictException">It's over, or it goes on in another process.</exception>
    public async Task<RunView> CancelAsync(string id, string account, CancellationToken cancellationToken)
    {
        SyncRun run = await FindAsync(id, cancellationToken).ConfigureAwait(false);
        if (run.Outcome == SyncOutcome.Queued
            && await runner.CancelQueuedAsync(run.Id, account, cancellationToken).ConfigureAwait(false) is { } canceled)
        {
            return new RunView(canceled, Cancellable: false);
        }

        if (run.Outcome == SyncOutcome.OnHold)
        {
            try
            {
                SyncRun ended = await refresh.CancelHeldAsync(run.Id, account, cancellationToken).ConfigureAwait(false);
                // The queue waited for it: it goes on.
                await runner.TryStartNextAsync().ConfigureAwait(false);
                return new RunView(ended, Cancellable: false);
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
            run.Mode is not (SyncMode.RefreshVideo
                or SyncMode.RefreshChannel
                or SyncMode.RefreshChannelVideos
                or SyncMode.DownloadVideo)
                ? "Only a refresh or a download can be put on hold: the other runs can be canceled."
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
        new(run, run.Id == runningHere || run.Outcome is SyncOutcome.OnHold or SyncOutcome.Queued);

    /// <exception cref="NotFoundException">No such run.</exception>
    public async Task<RunView> GetRunAsync(string id, CancellationToken cancellationToken)
    {
        SyncRun run = await FindAsync(id, cancellationToken).ConfigureAwait(false);
        RunView view = View(run, runner.RunningId);
        if (run.Outcome != SyncOutcome.Queued)
        {
            return view;
        }

        IReadOnlyList<SyncRun> queue = await runs.GetQueuedRunsAsync(cancellationToken).ConfigureAwait(false);
        int index = queue
            .Select(r => r.Id)
            .ToList()
            .IndexOf(run.Id);
        return view with { Position = index < 0 ? null : index + 1 };
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
