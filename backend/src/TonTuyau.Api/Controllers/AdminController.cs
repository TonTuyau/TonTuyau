using System.ComponentModel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TonTuyau.Api.Contracts;
using TonTuyau.Api.Errors;
using TonTuyau.Application.Dashboard;

namespace TonTuyau.Api.Controllers;

/// <summary>The admin area (1.2): any logged-in account; 401 for anyone else.</summary>
[ApiController]
[Authorize]
[Route("api/admin")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, Problems.ContentType)]
public sealed class AdminController(DashboardService dashboard) : ControllerBase
{
    /// <summary>The dashboard: the library's totals and play time, every channel.</summary>
    [HttpGet("dashboard")]
    [ProducesResponseType<DashboardResponse>(StatusCodes.Status200OK)]
    public async Task<DashboardResponse> GetDashboardAsync(CancellationToken cancellationToken) =>
        (DashboardResponse)await dashboard
            .GetAsync(cancellationToken)
            .ConfigureAwait(false);

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
