using System.ComponentModel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TonTuyau.Api.Contracts;
using TonTuyau.Api.Errors;
using TonTuyau.Application.Dashboard;
using TonTuyau.Application.Paging;

namespace TonTuyau.Api.Controllers;

/// <summary>The admin area (1.2): any logged-in account; 401 for anyone else.</summary>
[ApiController]
[Authorize]
[Route("api/admin")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, Problems.ContentType)]
public sealed class AdminController(DashboardService dashboard) : ControllerBase
{
    private const string ChannelSorts =
        "name | videos | shorts | streams | gone | private | unlisted | retryLater | notChecked | total (default).";

    /// <summary>The dashboard: the library's totals and play time.</summary>
    [HttpGet("dashboard")]
    [ProducesResponseType<DashboardResponse>(StatusCodes.Status200OK)]
    public async Task<DashboardResponse> GetDashboardAsync(CancellationToken cancellationToken) =>
        (DashboardResponse)await dashboard
            .GetAsync(cancellationToken)
            .ConfigureAwait(false);

    /// <summary>
    /// The dashboard's channels with their counts, a page at a time (alpha.3): filtered by name and to those with
    /// videos gone from YouTube, sorted by a column.
    /// </summary>
    [HttpGet("dashboard/channels")]
    [ProducesResponseType<Page<ChannelTotals>>(StatusCodes.Status200OK)]
    [ProducesResponseType<HttpValidationProblemDetails>(StatusCodes.Status400BadRequest, Problems.ContentType)]
    public async Task<Page<ChannelTotals>> GetChannelsAsync(
        [FromQuery, Description("Words of the channel's name, accents and case ignored, 1–100 characters.")] string? q,
        [FromQuery, Description(ChannelSorts)] string? sort,
        [FromQuery, Description("asc | desc; default: asc for name, desc for a count.")] string? order,
        [FromQuery, Description("true: only the channels with videos gone from YouTube.")] bool? gone,
        [FromQuery, Description("The previous page's nextCursor.")] string? cursor,
        [FromQuery, Description("1–100 (default 24).")] int? size,
        CancellationToken cancellationToken)
    {
        ResultPage<ChannelTotalsView> page = await dashboard
            .ListChannelsAsync(new DashboardChannelsRequest(q, sort, order, gone, cursor, size), cancellationToken)
            .ConfigureAwait(false);
        return new Page<ChannelTotals>([.. page.Items.Select(c => (ChannelTotals)c)], page.NextCursor, page.Total);
    }

    /// <summary>The dashboard's import history: the new videos of a period, per run, day, week or month.</summary>
    [HttpGet("dashboard/imports")]
    [ProducesResponseType<ImportHistoryResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<HttpValidationProblemDetails>(StatusCodes.Status400BadRequest, Problems.ContentType)]
    public async Task<ImportHistoryResponse> GetImportsAsync(
        [FromQuery, Description("day | week | month (default) | year | all.")] string? period,
        CancellationToken cancellationToken) =>
        (ImportHistoryResponse)await dashboard
            .GetImportsAsync(new ImportHistoryRequest(period), cancellationToken)
            .ConfigureAwait(false);
}
