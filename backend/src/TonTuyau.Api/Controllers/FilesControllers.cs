using Microsoft.AspNetCore.Mvc;
using TonTuyau.Api.Files;
using TonTuyau.Application.Media;

namespace TonTuyau.Api.Controllers;

[ApiController]
[Route("media")]
[ProducesResponseType<HttpValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
public sealed class MediaController(MediaService media) : ControllerBase
{
    /// <summary>The original file, with range requests (seeking).</summary>
    [HttpGet("{id}")]
    [HttpHead("{id}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status206PartialContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status416RangeNotSatisfiable, "application/problem+json")]
    public async Task<IActionResult> GetAsync(string id, CancellationToken cancellationToken)
    {
        MediaFile file = await media.GetVideoFileAsync(id, cancellationToken).ConfigureAwait(false);
        return new StoredFileResult(file.File, file.ContentType) { AcceptRanges = true };
    }

    /// <summary>The same file as an attachment, named after the video's title.</summary>
    [HttpGet("{id}/download")]
    [HttpHead("{id}/download")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status206PartialContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status416RangeNotSatisfiable, "application/problem+json")]
    public async Task<IActionResult> DownloadAsync(string id, CancellationToken cancellationToken)
    {
        MediaFile file = await media.GetVideoFileAsync(id, cancellationToken).ConfigureAwait(false);
        return new StoredFileResult(file.File, file.ContentType)
        {
            AcceptRanges = true,
            ContentDisposition = file.DownloadName.ContentDisposition,
        };
    }
}

[ApiController]
[Route("art")]
[ProducesResponseType<HttpValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
public sealed class ArtController(MediaService media) : ControllerBase
{
    /// <summary>
    /// A day in the browser's cache, then revalidated with the ETag (a 304 when unchanged): a refresh may replace
    /// an image under the same URL.
    /// </summary>
    public const string CacheControl = "public, max-age=86400";

    /// <summary>A video's thumbnail.</summary>
    [HttpGet("videos/{id}")]
    [HttpHead("videos/{id}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status304NotModified)]
    public async Task<IActionResult> VideoAsync(string id, CancellationToken cancellationToken) =>
        Image(await media.GetThumbnailAsync(id, cancellationToken).ConfigureAwait(false));

    /// <summary>A resized version of a video's thumbnail (WebP): 360 or 720 px wide; any other width is a 404.</summary>
    [HttpGet("videos/{id}/{width}")]
    [HttpHead("videos/{id}/{width}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status304NotModified)]
    public async Task<IActionResult> VideoVersionAsync(string id, string width, CancellationToken cancellationToken) =>
        Image(await media.GetThumbnailVersionAsync(id, width, cancellationToken).ConfigureAwait(false));

    /// <summary>
    /// A resized version of a channel's thumb (48, 96, 176, 320 px) or banner (1280 px), WebP; any other width is a 404.
    /// </summary>
    [HttpGet("channels/{id}/{kind}/{width}")]
    [HttpHead("channels/{id}/{kind}/{width}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status304NotModified)]
    public async Task<IActionResult> ChannelVersionAsync(
        string id,
        string kind,
        string width,
        CancellationToken cancellationToken) =>
        Image(await media.GetChannelArtVersionAsync(id, kind, width, cancellationToken).ConfigureAwait(false));

    /// <summary>A channel's thumb or banner (any other kind: 404).</summary>
    [HttpGet("channels/{id}/{kind}")]
    [HttpHead("channels/{id}/{kind}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status304NotModified)]
    public async Task<IActionResult> ChannelAsync(string id, string kind, CancellationToken cancellationToken) =>
        Image(await media.GetChannelArtAsync(id, kind, cancellationToken).ConfigureAwait(false));

    private static StoredFileResult Image(ArtFile art) =>
        new(art.File, art.ContentType) { CacheControl = CacheControl };
}
