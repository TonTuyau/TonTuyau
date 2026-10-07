using System.Globalization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Net.Http.Headers;
using TonTuyau.Application.Media;
using TonTuyau.Application.Sync;

namespace TonTuyau.Api.Files;

/// <summary>
/// Sends a file found by a service (never a path from the URL): GET and HEAD, ETag + Last-Modified, 304 for a copy
/// the client already has, and for media one byte range per request (206, or 416 through the error middleware).
/// </summary>
internal sealed class StoredFileResult(StoredFile file, string contentType) : IActionResult
{
    /// <summary>Media: <c>Accept-Ranges: bytes</c> and range requests (seeking).</summary>
    public bool AcceptRanges { get; init; }

    public string? ContentDisposition { get; init; }

    public string? CacheControl { get; init; }

    public async Task ExecuteResultAsync(ActionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        HttpRequest request = context.HttpContext.Request;
        HttpResponse response = context.HttpContext.Response;
        string etag = ByteRanges.ETag(file);
        DateTimeOffset lastModified = ByteRanges.TruncateToSeconds(file.LastModified);

        // Conditions first (RFC 9110 §13.2.2), then the range, which may throw (416) before any header is set:
        // the error answer carries none of this file's headers.
        bool notModified = IsNotModified(request, etag, lastModified);
        ByteRange? range = AcceptRanges && !notModified
            ? ByteRanges.Select(request.Headers.Range, request.Headers.IfRange, file)
            : null;

        response.Headers.ETag = etag;
        response.Headers.LastModified = lastModified.ToString("R", CultureInfo.InvariantCulture);
        if (CacheControl is not null)
        {
            response.Headers.CacheControl = CacheControl;
        }

        if (AcceptRanges)
        {
            response.Headers.AcceptRanges = "bytes";
        }

        if (notModified)
        {
            response.StatusCode = StatusCodes.Status304NotModified;
            return;
        }

        if (ContentDisposition is not null)
        {
            response.Headers.ContentDisposition = ContentDisposition;
        }

        response.ContentType = contentType;
        long offset = 0;
        long count = file.Length;
        if (range is null)
        {
            response.StatusCode = StatusCodes.Status200OK;
        }
        else
        {
            response.StatusCode = StatusCodes.Status206PartialContent;
            response.Headers.ContentRange = range.ContentRange(file.Length);
            offset = range.Start;
            count = range.Length;
        }

        response.ContentLength = count;
        if (HttpMethods.IsHead(request.Method) || count == 0)
        {
            return;
        }

        await response
            .SendFileAsync(file.FullPath, offset, count, context.HttpContext.RequestAborted)
            .ConfigureAwait(false);
    }

    /// <summary>RFC 9110 §13.1.2: If-None-Match (weak comparison) wins over If-Modified-Since.</summary>
    private static bool IsNotModified(HttpRequest request, string etag, DateTimeOffset lastModified)
    {
        if (request.Headers.IfNoneMatch.Count > 0)
        {
            EntityTagHeaderValue current = new(etag);
            return EntityTagHeaderValue.TryParseList(request.Headers.IfNoneMatch, out IList<EntityTagHeaderValue>? tags)
                && tags.Any(t => t.Equals(EntityTagHeaderValue.Any) || t.Compare(current, useStrongComparison: false));
        }

        return request.GetTypedHeaders().IfModifiedSince is { } since && lastModified <= since;
    }
}
