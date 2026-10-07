using System.ComponentModel;
using Microsoft.AspNetCore.Mvc;
using TonTuyau.Api.Contracts;
using TonTuyau.Application.Browse;

namespace TonTuyau.Api.Controllers;

[ApiController]
[Route("api/search")]
[ProducesResponseType<HttpValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
public sealed class SearchController(SearchService search) : ControllerBase
{
    /// <summary>
    /// Searches titles, channel names, tags and descriptions. The answer's shape depends on kind: all →
    /// SearchAllResponse, videos → page of VideoCard, channels → page of ChannelCard.
    /// </summary>
    [HttpGet]
    [ProducesResponseType<SearchAllResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<Page<VideoCard>>(StatusCodes.Status200OK)]
    [ProducesResponseType<Page<ChannelCard>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> SearchAsync(
        [FromQuery, Description("What to look for, 1–200 characters.")] string? q,
        [FromQuery, Description("all (default) | videos | channels.")] string? kind,
        [FromQuery, Description("Videos only: video | short | stream.")] string? type,
        [FromQuery, Description("Videos only: a channel id.")] string? channel,
        [FromQuery, Description("Videos only: published in the last today (24 h) | week | month (30 days) | year.")]
        string? published,
        [FromQuery, Description("Videos only: short (< 4 min) | medium (4–20) | long (> 20).")] string? duration,
        [FromQuery, Description("relevance (default) | latest | popular.")] string? sort,
        [FromQuery, Description("nextCursor of the previous page.")] string? cursor,
        [FromQuery, Description("1–100 (default 24).")] int? size,
        CancellationToken cancellationToken)
    {
        VideoSearchRequest request = new(
            q,
            kind,
            type,
            channel,
            published,
            duration,
            sort,
            cursor,
            size);
        return Ok(ApiMapper.ToDto(await search.SearchAsync(request, cancellationToken).ConfigureAwait(false)));
    }

    /// <summary>As-you-type suggestions: channels first, then video titles (accents ignored).</summary>
    [HttpGet("suggest")]
    [ProducesResponseType<SuggestResponse>(StatusCodes.Status200OK)]
    public async Task<SuggestResponse> SuggestAsync(
        [FromQuery, Description("What was typed so far, 1–100 characters.")] string? q,
        [FromQuery, Description("1–20 (default 8).")] int? size,
        CancellationToken cancellationToken)
    {
        SuggestRequest request = new(q, size);
        return ApiMapper.ToDto(await search.SuggestAsync(request, cancellationToken).ConfigureAwait(false));
    }
}
