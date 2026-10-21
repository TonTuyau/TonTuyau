using Microsoft.AspNetCore.Mvc;
using TonTuyau.Api.Files;
using TonTuyau.Application.Media;
using TonTuyau.Application.Streaming;

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

/// <summary>
/// A video played in parts (2.0, C3, question 9): an HLS playlist, the init segment and the parts, each copied out of
/// the file by ffmpeg when it's asked for. <c>session</c> is the viewing's (made by the player): one ffmpeg run each,
/// stopped when it's closed or after a minute without anything asked. 409 when the file can't be played in parts:
/// the player plays it whole (<c>/media/{id}</c>).
/// </summary>
[ApiController]
[Route("media/{id}/hls/{session}")]
[ProducesResponseType<HttpValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
public sealed class StreamController(StreamingService streaming) : ControllerBase
{
    /// <summary>The viewer's own (a session of theirs): not kept by a cache shared by several viewers.</summary>
    public const string CacheControl = "private, no-cache";

    /// <summary>The playlist: the parts, keyframe to keyframe, about 6 s each.</summary>
    [HttpGet("index.m3u8")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> PlaylistAsync(string id, string session, CancellationToken cancellationToken)
    {
        string playlist = await streaming.GetPlaylistAsync(id, session, cancellationToken).ConfigureAwait(false);
        Response.Headers.CacheControl = CacheControl;
        return Content(playlist, "application/vnd.apple.mpegurl");
    }

    /// <summary>The init segment: the MP4 header every part needs before it.</summary>
    [HttpGet(StreamingService.InitName)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> InitAsync(string id, string session, CancellationToken cancellationToken)
    {
        byte[] init = await streaming.GetInitAsync(id, session, cancellationToken).ConfigureAwait(false);
        Response.Headers.CacheControl = CacheControl;
        return File(init, "video/mp4");
    }

    /// <summary>A part, from its index in the playlist.</summary>
    [HttpGet("{index:int}" + StreamingService.PartExtension)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> PartAsync(
        string id,
        string session,
        int index,
        CancellationToken cancellationToken)
    {
        byte[] part = await streaming.GetPartAsync(id, session, index, cancellationToken).ConfigureAwait(false);
        Response.Headers.CacheControl = CacheControl;
        return File(part, "video/iso.segment");
    }

    /// <summary>The viewer left: its ffmpeg stops now.</summary>
    [HttpDelete]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public IActionResult Close(string session)
    {
        streaming.Close(session);
        return NoContent();
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
