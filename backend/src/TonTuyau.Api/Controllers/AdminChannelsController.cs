using System.ComponentModel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TonTuyau.Api.Contracts;
using TonTuyau.Api.Errors;
using TonTuyau.Application.ChannelAdmin;
using TonTuyau.Application.Paging;

namespace TonTuyau.Api.Controllers;

/// <summary>
/// A channel's admin page (1.5): the channel and its videos as TonTuyau knows them on YouTube. Refreshing them is a
/// task (<c>POST /api/admin/tasks/{task}/runs?target=</c>). An admin's session only.
/// </summary>
[ApiController]
[Authorize]
[Route("api/admin/channels")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, Problems.ContentType)]
public sealed class AdminChannelsController(ChannelAdminService channels) : ControllerBase
{
    /// <summary>The channel, its state on YouTube, how many of its videos are in each state. 404 when unknown.</summary>
    [HttpGet("{id}")]
    [ProducesResponseType<AdminChannelResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<HttpValidationProblemDetails>(StatusCodes.Status400BadRequest, Problems.ContentType)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, Problems.ContentType)]
    public async Task<AdminChannelResponse> GetAsync(string id, CancellationToken cancellationToken) =>
        (AdminChannelResponse)await channels
            .GetAsync(id, cancellationToken)
            .ConfigureAwait(false);

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
        CancellationToken cancellationToken)
    {
        ResultPage<AdminVideoView> page = await channels
            .ListVideosAsync(new AdminVideosRequest(id, state, q, cursor, size), cancellationToken)
            .ConfigureAwait(false);
        return new Page<AdminVideoResponse>(
            [.. page.Items.Select(v => (AdminVideoResponse)v)],
            page.NextCursor,
            page.Total);
    }
}
