using Microsoft.AspNetCore.Mvc;
using TonTuyau.Api.Contracts;
using TonTuyau.Api.Errors;
using TonTuyau.Application.Sync;
using TonTuyau.Application.Tasks;

namespace TonTuyau.Api.Cli;

/// <summary>
/// The command line's endpoints (1.4): <c>tontuyau import</c> (2.0), <c>tontuyau index rebuild</c> and (1.5)
/// <c>tontuyau youtube …</c> start their run here,
/// in the backend (its log, the Tasks page), follow its log and cancel it. Through the backend's Unix socket only
/// (<see cref="CliOnlyFilter"/>); not under /api (the front doesn't forward them) nor in the OpenAPI document.
/// </summary>
[ApiController]
[Route("cli")]
[ApiExplorerSettings(IgnoreApi = true)]
[ServiceFilter<CliOnlyFilter>]
public sealed class CliController(SyncRunner runner, TaskService tasks) : ControllerBase
{
    /// <summary>The command line's name in the runs' logs, when it cancels one.</summary>
    public const string Name = "the command line";

    [HttpPost("import")]
    public Task<IActionResult> ImportAsync() => StartAsync(() => runner.StartImportAsync(SyncTrigger.Cli));

    [HttpPost("index/rebuild")]
    public Task<IActionResult> RebuildAsync() => StartAsync(() => runner.StartRebuildAsync(SyncTrigger.Cli));

    [HttpPost("youtube/video/{id}")]
    public Task<IActionResult> RefreshVideoAsync(string id) =>
        StartAsync(() => runner.StartRefreshAsync(SyncMode.RefreshVideo, id, SyncTrigger.Cli));

    [HttpPost("youtube/channel/{id}")]
    public Task<IActionResult> RefreshChannelAsync(string id) =>
        StartAsync(() => runner.StartRefreshAsync(SyncMode.RefreshChannel, id, SyncTrigger.Cli));

    [HttpPost("youtube/channel-videos/{id}")]
    public Task<IActionResult> RefreshChannelVideosAsync(string id) =>
        StartAsync(() => runner.StartRefreshAsync(SyncMode.RefreshChannelVideos, id, SyncTrigger.Cli));

    /// <summary>The run's whole record, for the command line's report.</summary>
    [HttpGet("runs/{id}")]
    public async Task<SyncRun> GetRunAsync(string id, CancellationToken cancellationToken) =>
        (await tasks
            .GetRunAsync(id, cancellationToken)
            .ConfigureAwait(false))
            .Run;

    [HttpGet("runs/{id}/log")]
    public async Task<RunLogResponse> GetLogAsync(string id, long? after, CancellationToken cancellationToken) =>
        (RunLogResponse)await tasks
            .GetLogAsync(id, new RunLogRequest(after, TaskService.MaxLogLinesPerPage), cancellationToken)
            .ConfigureAwait(false);

    /// <summary>Ctrl-C: the run stops soon after. 409 when it's over already.</summary>
    [HttpPost("runs/{id}/cancel")]
    public IActionResult Cancel(string id) =>
        runner.Cancel(id, Name)
            ? Accepted()
            : new ProblemResult(Problems.Conflict("This run isn't going on in the backend."));

    /// <summary>202 and the run's id; 409 when the lock is busy or the run refused (the exit codes 3 and 4).</summary>
    private async Task<IActionResult> StartAsync(Func<Task<StartedRun>> start)
    {
        try
        {
            StartedRun run = await start().ConfigureAwait(false);
            return Accepted(new CliStarted(run.Id));
        }
        catch (SyncBusyException ex)
        {
            return new ProblemResult(Problems.SyncBusy(ex.Message));
        }
        catch (SyncRefusedException ex)
        {
            return new ProblemResult(Problems.SyncRefused(ex.Message));
        }
    }
}
