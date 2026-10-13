using System.ComponentModel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TonTuyau.Api.Contracts;
using TonTuyau.Api.Errors;
using TonTuyau.Application.Paging;
using TonTuyau.Application.Tasks;

namespace TonTuyau.Api.Controllers;

/// <summary>The admin's Tasks page (1.4): the tasks, the run history, each run's log. Any logged-in account.</summary>
[ApiController]
[Authorize]
[Route("api/admin")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, Problems.ContentType)]
public sealed class TasksController(TaskService tasks) : ControllerBase
{
    /// <summary>The incremental sync, the full sync and applying deletions: when they run, how they last ran.</summary>
    [HttpGet("tasks")]
    [ProducesResponseType<TasksResponse>(StatusCodes.Status200OK)]
    public async Task<TasksResponse> GetTasksAsync(CancellationToken cancellationToken) =>
        (TasksResponse)await tasks
            .GetTasksAsync(cancellationToken)
            .ConfigureAwait(false);

    /// <summary>The run history, latest first.</summary>
    [HttpGet("runs")]
    [ProducesResponseType<Page<RunResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<HttpValidationProblemDetails>(StatusCodes.Status400BadRequest, Problems.ContentType)]
    public async Task<Page<RunResponse>> GetRunsAsync(
        [FromQuery, Description("incremental | full | applyDeletions; every task when absent.")] string? task,
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
}
