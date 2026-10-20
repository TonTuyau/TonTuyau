using System.ComponentModel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TonTuyau.Api.Contracts;
using TonTuyau.Api.Errors;
using TonTuyau.Application.Paging;
using TonTuyau.Application.Tasks;

namespace TonTuyau.Api.Controllers;

/// <summary>
/// The admin's Tasks page (1.4): the tasks, the run history, each run's log; starting a task, canceling a run. Any
/// logged-in account.
/// </summary>
[ApiController]
[Authorize]
[Route("api/admin")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, Problems.ContentType)]
public sealed class TasksController(TaskService tasks) : ControllerBase
{
    /// <summary>The TubeArchivist import (2.0): how it last ran, or why it can't start.</summary>
    [HttpGet("tasks")]
    [ProducesResponseType<TasksResponse>(StatusCodes.Status200OK)]
    public async Task<TasksResponse> GetTasksAsync(CancellationToken cancellationToken) =>
        (TasksResponse)await tasks
            .GetTasksAsync(cancellationToken)
            .ConfigureAwait(false);

    /// <summary>
    /// Asks for a task (2.0: queued, started at once when nothing else goes on; it goes on in the background). The
    /// import (2.0) adds what TubeArchivist has and TonTuyau doesn't; the refreshes (1.5) read their target on YouTube;
    /// a download (2.0, "Add a video") downloads a video into the library. 409 when TubeArchivist isn't set up, the
    /// target isn't one of the library (a refresh), or is already, with its file (a download).
    /// </summary>
    [HttpPost("tasks/{task}/runs")]
    [ProducesResponseType<RunResponse>(StatusCodes.Status202Accepted)]
    [ProducesResponseType<HttpValidationProblemDetails>(StatusCodes.Status400BadRequest, Problems.ContentType)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, Problems.ContentType)]
    public async Task<AcceptedResult> StartAsync(
        [Description("import | refreshVideo | refreshChannel | refreshChannelVideos | downloadVideo.")]
        string task,
        [FromQuery]
        [Description("Refreshes, downloadVideo only: the video's or channel's YouTube id (downloadVideo: or a link).")]
        string? target,
        CancellationToken cancellationToken)
    {
        RunResponse run = (RunResponse)await tasks
            .StartAsync(task, target, Account(), cancellationToken)
            .ConfigureAwait(false);
        return Accepted(new Uri($"/api/admin/runs/{run.Id}", UriKind.Relative), run);
    }

    /// <summary>
    /// Resumes a refresh on hold now (1.5) instead of at the end of its cool-down: the same run goes on. 409 when it
    /// isn't on hold, or a run is going on.
    /// </summary>
    [HttpPost("runs/{id}/resume")]
    [ProducesResponseType<RunResponse>(StatusCodes.Status202Accepted)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, Problems.ContentType)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, Problems.ContentType)]
    public async Task<AcceptedResult> ResumeAsync(string id, CancellationToken cancellationToken)
    {
        RunResponse run = (RunResponse)await tasks
            .ResumeAsync(id, Account(), cancellationToken)
            .ConfigureAwait(false);
        return Accepted(new Uri($"/api/admin/runs/{run.Id}", UriKind.Relative), run);
    }

    /// <summary>
    /// Puts the refresh going on on hold now, by hand (1.5): it stops at once and lets go of the lock, keeps what it has
    /// left to read (the item it was reading included), and waits for Resume now: no cool-down. 409 when it isn't a
    /// refresh going on in this backend.
    /// </summary>
    [HttpPost("runs/{id}/hold")]
    [ProducesResponseType<RunResponse>(StatusCodes.Status202Accepted)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, Problems.ContentType)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, Problems.ContentType)]
    public async Task<AcceptedResult> HoldAsync(string id, CancellationToken cancellationToken)
    {
        RunResponse run = (RunResponse)await tasks
            .HoldAsync(id, Account(), cancellationToken)
            .ConfigureAwait(false);
        return Accepted(new Uri($"/api/admin/runs/{run.Id}", UriKind.Relative), run);
    }

    /// <summary>
    /// Asks the run going on to stop: it ends canceled soon after (between two requests), and its log says who asked.
    /// A refresh on hold (1.5) is canceled at once. 409 when it's over, or goes on in another process.
    /// </summary>
    [HttpPost("runs/{id}/cancel")]
    [ProducesResponseType<RunResponse>(StatusCodes.Status202Accepted)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, Problems.ContentType)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, Problems.ContentType)]
    public async Task<AcceptedResult> CancelAsync(string id, CancellationToken cancellationToken)
    {
        RunResponse run = (RunResponse)await tasks
            .CancelAsync(id, Account(), cancellationToken)
            .ConfigureAwait(false);
        return Accepted(new Uri($"/api/admin/runs/{run.Id}", UriKind.Relative), run);
    }

    /// <summary>The run history, latest first.</summary>
    [HttpGet("runs")]
    [ProducesResponseType<Page<RunResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<HttpValidationProblemDetails>(StatusCodes.Status400BadRequest, Problems.ContentType)]
    public async Task<Page<RunResponse>> GetRunsAsync(
        [FromQuery, Description("A task's name (see POST tasks/{task}/runs); every task when absent.")] string? task,
        [FromQuery, Description("The previous page's nextCursor: the runs started before that one.")] string? before,
        [FromQuery, Description("1 to 100, default 20.")] int? size,
        CancellationToken cancellationToken)
    {
        ResultPage<RunView> page = await tasks
            .GetRunsAsync(new RunListRequest(task, before, size), cancellationToken)
            .ConfigureAwait(false);
        return new Page<RunResponse>([.. page.Items.Select(r => (RunResponse)r)], page.NextCursor, page.Total);
    }

    /// <summary>A run: its counts, notes, error.</summary>
    [HttpGet("runs/{id}")]
    [ProducesResponseType<RunResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, Problems.ContentType)]
    public async Task<RunResponse> GetRunAsync(string id, CancellationToken cancellationToken) =>
        (RunResponse)await tasks
            .GetRunAsync(id, cancellationToken)
            .ConfigureAwait(false);

    /// <summary>A run's log, oldest line first: while it goes on, ask again for the lines after the last one.</summary>
    [HttpGet("runs/{id}/log")]
    [ProducesResponseType<RunLogResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<HttpValidationProblemDetails>(StatusCodes.Status400BadRequest, Problems.ContentType)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, Problems.ContentType)]
    public async Task<RunLogResponse> GetLogAsync(
        string id,
        [FromQuery, Description("The last line's id: the lines after it. 0 (default): from the first.")] long? after,
        [FromQuery, Description("1 to 2000, default 500.")] int? size,
        CancellationToken cancellationToken) =>
        (RunLogResponse)await tasks
            .GetLogAsync(id, new RunLogRequest(after, size), cancellationToken)
            .ConfigureAwait(false);

    /// <summary>The logged-in account's name ([Authorize]: there is one).</summary>
    private string Account() =>
        User.Identity?.Name ?? throw new InvalidOperationException("A logged-in request without a name.");
}
