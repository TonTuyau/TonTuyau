using System.ComponentModel;
using Microsoft.AspNetCore.Mvc;
using TonTuyau.Api.Contracts;
using TonTuyau.Application.Browse;
using TonTuyau.Application.Comments;

namespace TonTuyau.Api.Controllers;

// HTTP binding only: query and route values go to one service call, its answer comes back as a contract DTO.
// Every rule (defaults, allowed values, 400s and 404s) lives in the services and their validators.

[ApiController]
[Route("api/home")]
[ProducesResponseType<HttpValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
public sealed class HomeController(VideoService videos) : ControllerBase
{
    /// <summary>The home page: the latest published of each type.</summary>
    [HttpGet]
    [ProducesResponseType<HomeResponse>(StatusCodes.Status200OK)]
    public async Task<HomeResponse> GetAsync(
        [FromQuery, Description("Videos per section, 1–100 (default 24).")] int? size,
        CancellationToken cancellationToken) =>
        ApiMapper.ToDto(await videos.HomeAsync(new HomeRequest(size), cancellationToken).ConfigureAwait(false));
}

[ApiController]
[Route("api/videos")]
[ProducesResponseType<HttpValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
public sealed class VideosController(VideoService videos, CommentService comments) : ControllerBase
{
    /// <summary>Every video, latest published first by default.</summary>
    [HttpGet]
    [ProducesResponseType<Page<VideoCard>>(StatusCodes.Status200OK)]
    public async Task<Page<VideoCard>> ListAsync(
        [FromQuery, Description("video | short | stream (default: all types).")] string? type,
        [FromQuery, Description("latest (default) | popular | oldest.")] string? sort,
        [FromQuery, Description("A video id left out of the items and the total (the short being watched).")]
        string? exclude,
        [FromQuery, Description("nextCursor of the previous page.")] string? cursor,
        [FromQuery, Description("1–100 (default 24).")] int? size,
        CancellationToken cancellationToken)
    {
        VideoListRequest request = new(type, sort, exclude, cursor, size);
        return ApiMapper.ToDto(await videos.ListAsync(request, cancellationToken).ConfigureAwait(false));
    }

    /// <summary>The watch page.</summary>
    [HttpGet("{id}")]
    [ProducesResponseType<VideoDetail>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<VideoDetail> GetAsync(string id, CancellationToken cancellationToken) =>
        ApiMapper.ToDto(await videos.GetAsync(id, cancellationToken).ConfigureAwait(false));

    /// <summary>
    /// Its comments as TonTuyau read them on YouTube (1.5): a page of threads, each with its answers. Never read:
    /// <c>readAt</c> null and no threads.
    /// </summary>
    [HttpGet("{id}/comments")]
    [ProducesResponseType<CommentsResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<CommentsResponse> GetCommentsAsync(
        string id,
        [FromQuery, Description("top (YouTube's order, the default) | newest.")] string? sort,
        [FromQuery, Description("The previous page's nextCursor.")] string? cursor,
        [FromQuery, Description("Threads per page, 1–100 (default 20).")] int? size,
        CancellationToken cancellationToken) =>
        (CommentsResponse)await comments
            .GetAsync(new CommentsRequest(id, sort, cursor, size), cancellationToken)
            .ConfigureAwait(false);
}

[ApiController]
[Route("api/channels")]
[ProducesResponseType<HttpValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
public sealed class ChannelsController(ChannelService channels) : ControllerBase
{
    /// <summary>Every channel, by name by default.</summary>
    [HttpGet]
    [ProducesResponseType<Page<ChannelCard>>(StatusCodes.Status200OK)]
    public async Task<Page<ChannelCard>> ListAsync(
        [FromQuery, Description("As-you-type filter on the name (accents ignored).")] string? q,
        [FromQuery, Description("name (default) | subscribers.")] string? sort,
        [FromQuery, Description("nextCursor of the previous page.")] string? cursor,
        [FromQuery, Description("1–100 (default 24).")] int? size,
        CancellationToken cancellationToken)
    {
        ChannelListRequest request = new(q, sort, cursor, size);
        return ApiMapper.ToDto(await channels.ListAsync(request, cancellationToken).ConfigureAwait(false));
    }

    /// <summary>A channel's page. Its tabs are the types whose videoCounts are above 0.</summary>
    [HttpGet("{id}")]
    [ProducesResponseType<ChannelDetail>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<ChannelDetail> GetAsync(string id, CancellationToken cancellationToken) =>
        ApiMapper.ToDto(await channels.GetAsync(id, cancellationToken).ConfigureAwait(false));

    /// <summary>One tab of a channel.</summary>
    [HttpGet("{id}/videos")]
    [ProducesResponseType<Page<VideoCard>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<Page<VideoCard>> ListVideosAsync(
        string id,
        [FromQuery, Description("video (default) | short | stream.")] string? type,
        [FromQuery, Description("latest (default) | popular | oldest.")] string? sort,
        [FromQuery, Description("A video id left out of the items and the total (the one being watched).")]
        string? exclude,
        [FromQuery, Description("nextCursor of the previous page.")] string? cursor,
        [FromQuery, Description("1–100 (default 24).")] int? size,
        CancellationToken cancellationToken)
    {
        ChannelVideosRequest request = new(id, type, sort, exclude, cursor, size);
        return ApiMapper.ToDto(await channels.ListVideosAsync(request, cancellationToken).ConfigureAwait(false));
    }
}
