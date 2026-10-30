using System.ComponentModel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TonTuyau.Api.Contracts;
using TonTuyau.Api.Errors;
using TonTuyau.Application.ChannelAdmin;
using TonTuyau.Application.Library;
using TonTuyau.Application.Paging;

namespace TonTuyau.Api.Controllers;

/// <summary>
/// A channel's admin page (1.5): the channel and its videos as TonTuyau knows them on YouTube, and its subscription
/// (2.0, B4). Refreshing them is a task (<c>POST /api/admin/tasks/{task}/runs?target=</c>). An admin's session only.
/// </summary>
[ApiController]
[Authorize]
[Route("api/admin/channels")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, Problems.ContentType)]
public sealed class AdminChannelsController(ChannelAdminService channels) : ControllerBase
{
    /// <summary>A subscription's body is three booleans: well under a kilobyte.</summary>
    private const int MaxRequestBytes = 1024;

    /// <summary>The channel, its state on YouTube, how many of its videos are in each state. 404 when unknown.</summary>
    [HttpGet("{id}")]
    [ProducesResponseType<AdminChannelResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<HttpValidationProblemDetails>(StatusCodes.Status400BadRequest, Problems.ContentType)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, Problems.ContentType)]
    public async Task<AdminChannelResponse> GetAsync(string id, CancellationToken cancellationToken) =>
        (AdminChannelResponse)await channels
            .GetAsync(id, cancellationToken)
            .ConfigureAwait(false);

    /// <summary>
    /// Subscribes to the channel (contract v2.3), or changes the tabs of its subscription. 400 when a tab is missing or
    /// none is ticked; 404 when the channel is unknown.
    /// </summary>
    [HttpPut("{id}/subscription")]
    [Consumes("application/json")]
    [RequestSizeLimit(MaxRequestBytes)]
    [ProducesResponseType<SubscriptionResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<HttpValidationProblemDetails>(StatusCodes.Status400BadRequest, Problems.ContentType)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, Problems.ContentType)]
    public async Task<SubscriptionResponse> SubscribeAsync(
        string id,
        SubscriptionRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return (SubscriptionResponse)await channels
            .SubscribeAsync(id, request.Videos, request.Shorts, request.Streams, Account(), cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Unsubscribes (contract v2.3): the channel and its videos stay, nothing more is downloaded. 204, subscribed or
    /// not; 404 when the channel is unknown.
    /// </summary>
    [HttpDelete("{id}/subscription")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<HttpValidationProblemDetails>(StatusCodes.Status400BadRequest, Problems.ContentType)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, Problems.ContentType)]
    public async Task<IActionResult> UnsubscribeAsync(string id, CancellationToken cancellationToken)
    {
        await channels
            .UnsubscribeAsync(id, cancellationToken)
            .ConfigureAwait(false);
        return NoContent();
    }

    /// <summary>
    /// Its videos the YouTube syncs couldn't download (contract v2.4), the latest failed first: why, how many times.
    /// Try again: the task <c>downloadVideo</c> of one, <c>retryDownloads</c> of them all. 404 when it's unknown.
    /// </summary>
    [HttpGet("{id}/failed-downloads")]
    [ProducesResponseType<IReadOnlyList<DownloadFailureResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<HttpValidationProblemDetails>(StatusCodes.Status400BadRequest, Problems.ContentType)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, Problems.ContentType)]
    public async Task<IReadOnlyList<DownloadFailureResponse>> GetFailedDownloadsAsync(
        string id,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<DownloadFailure> failures = await channels
            .ListFailedDownloadsAsync(id, cancellationToken)
            .ConfigureAwait(false);
        return [.. failures.Select(f => (DownloadFailureResponse)f)];
    }

    /// <summary>
    /// Its videos whose files were purged on their own (contract v2.17), the latest published first: no sync downloads
    /// them, the library's lists leave them out. 404 when the channel is unknown.
    /// </summary>
    [HttpGet("{id}/blocked-videos")]
    [ProducesResponseType<IReadOnlyList<AdminVideoResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<HttpValidationProblemDetails>(StatusCodes.Status400BadRequest, Problems.ContentType)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, Problems.ContentType)]
    public async Task<IReadOnlyList<AdminVideoResponse>> GetBlockedVideosAsync(
        string id,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<AdminVideoView> blocked = await channels
            .ListSyncBlockedAsync(id, cancellationToken)
            .ConfigureAwait(false);
        return [.. blocked.Select(v => (AdminVideoResponse)v)];
    }

    /// <summary>
    /// Unblocks some of them, or all of them (contract v2.17): back in the library's lists, without their files. 204;
    /// 404 when the channel is unknown.
    /// </summary>
    [HttpDelete("{id}/blocked-videos")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<HttpValidationProblemDetails>(StatusCodes.Status400BadRequest, Problems.ContentType)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, Problems.ContentType)]
    public async Task<IActionResult> UnblockVideosAsync(
        string id,
        [FromQuery, Description("The videos to unblock, ids by commas; all of them when absent.")] string? videos,
        CancellationToken cancellationToken)
    {
        await channels
            .UnblockSyncAsync(id, videos, cancellationToken)
            .ConfigureAwait(false);
        return NoContent();
    }

    /// <summary>Its videos with their state on YouTube, latest published first. 404 when the channel is unknown.</summary>
    [HttpGet("{id}/videos")]
    [ProducesResponseType<Page<AdminVideoResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<HttpValidationProblemDetails>(StatusCodes.Status400BadRequest, Problems.ContentType)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, Problems.ContentType)]
    public async Task<Page<AdminVideoResponse>> GetVideosAsync(
        string id,
        [FromQuery, Description("notChecked | onYouTube | retryLater | gone; every state when absent.")] string? state,
        [FromQuery, Description("Words of the title, tags or description, 1–100 characters.")] string? q,
        [FromQuery, Description("The previous page's nextCursor.")] string? cursor,
        [FromQuery, Description("1–100 (default 24).")] int? size,
        [FromQuery, Description("true: only the videos whose syncs are blocked (contract v2.17).")] bool? blocked,
        CancellationToken cancellationToken)
    {
        ResultPage<AdminVideoView> page = await channels
            .ListVideosAsync(new AdminVideosRequest(id, state, q, cursor, size, blocked), cancellationToken)
            .ConfigureAwait(false);
        return new Page<AdminVideoResponse>(
            [.. page.Items.Select(v => (AdminVideoResponse)v)],
            page.NextCursor,
            page.Total);
    }

    /// <summary>The logged-in account's name ([Authorize]: there is one).</summary>
    private string Account() =>
        User.Identity?.Name ?? throw new InvalidOperationException("A logged-in request without a name.");
}
