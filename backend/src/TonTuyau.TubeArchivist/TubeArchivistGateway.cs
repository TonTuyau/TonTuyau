using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Polly.CircuitBreaker;
using Polly.Timeout;
using TonTuyau.Application.Library;
using TonTuyau.Application.TubeArchivist;
using TonTuyau.Application.Validation;
using TonTuyau.TubeArchivist.Dtos;
using TonTuyau.TubeArchivist.Mapping;
using TonTuyau.TubeArchivist.Validation;

namespace TonTuyau.TubeArchivist;

/// <summary>
/// Read-only TubeArchivist client. The HttpClient it gets is wrapped by <see cref="Http.ReadOnlyGuardHandler"/>,
/// so even a bug here can't send anything but GET/HEAD.
/// </summary>
internal sealed class TubeArchivistGateway(
    HttpClient http,
    IValidator<VideoDto> videoValidator,
    IValidator<ChannelDto> channelValidator) : ITubeArchivistGateway
{
    public async Task<TubeArchivistInfo> PingAsync(CancellationToken cancellationToken)
    {
        PingDto? ping = await GetJsonAsync<PingDto>("api/ping/", cancellationToken).ConfigureAwait(false);
        if (ping?.Response != "pong" || string.IsNullOrWhiteSpace(ping.Version))
        {
            throw new TubeArchivistProtocolException("Unexpected answer to /api/ping/.");
        }

        return new TubeArchivistInfo(ping.Version);
    }

    public async Task<LibraryStats> GetLibraryStatsAsync(CancellationToken cancellationToken)
    {
        VideoStatsDto stats = await GetJsonAsync<VideoStatsDto>("api/stats/video/", cancellationToken).ConfigureAwait(
            false)
            ?? throw new TubeArchivistProtocolException("Empty answer from /api/stats/video/.");

        // TA reports a zero count as null (e.g. "active_false": null).
        return new LibraryStats(
            Total: stats.DocCount ?? 0,
            Videos: stats.TypeVideos?.DocCount ?? 0,
            Shorts: stats.TypeShorts?.DocCount ?? 0,
            Streams: stats.TypeStreams?.DocCount ?? 0,
            Active: stats.ActiveTrue?.DocCount ?? 0,
            Inactive: stats.ActiveFalse?.DocCount ?? 0);
    }

    public async Task<long> GetChannelCountAsync(CancellationToken cancellationToken)
    {
        ChannelStatsDto? stats = await GetJsonAsync<ChannelStatsDto>("api/stats/channel/", cancellationToken)
            .ConfigureAwait(false);
        return stats?.DocCount
            ?? throw new TubeArchivistProtocolException("/api/stats/channel/: no doc_count in the answer.");
    }

    public async Task<long?> GetChannelVideoTotalAsync(string channelId, CancellationToken cancellationToken)
    {
        EnsureFormat(channelId, TaFormats.ChannelId(), nameof(channelId));
        JsonElement? json = await GetJsonOrNullAsync($"api/channel/{channelId}/aggs/", cancellationToken)
            .ConfigureAwait(false);
        if (json is null)
        {
            return null;
        }

        string where = $"/api/channel/{channelId}/aggs/";
        return Deserialize<ChannelAggsDto>(json.Value).TotalItems?.Value
            ?? throw new TubeArchivistProtocolException($"{where}: no total_items in the answer.");
    }

    public Task<Page<TaChannel>> GetChannelPageAsync(int page, CancellationToken cancellationToken)
    {
        EnsurePageNumber(page);
        return GetPageAsync(
            $"api/channel/?page={page.ToString(CultureInfo.InvariantCulture)}",
            page,
            ParseListedChannel,
            cancellationToken);
    }

    public Task<Page<Video>> GetVideoPageAsync(VideoPageQuery query, int page, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        EnsurePageNumber(page);
        return GetPageAsync(
            BuildVideoListUrl(query, page),
            page,
            ParseVideo,
            cancellationToken);
    }

    public async Task<Video?> GetVideoAsync(string videoId, CancellationToken cancellationToken)
    {
        EnsureFormat(videoId, TaFormats.VideoId(), nameof(videoId));
        JsonElement? json = await GetJsonOrNullAsync($"api/video/{videoId}/", cancellationToken).ConfigureAwait(false);
        return json is null ? null : ParseVideo(json.Value);
    }

    public async Task<Channel?> GetChannelAsync(string channelId, CancellationToken cancellationToken)
    {
        EnsureFormat(channelId, TaFormats.ChannelId(), nameof(channelId));
        JsonElement? json = await GetJsonOrNullAsync($"api/channel/{channelId}/", cancellationToken).ConfigureAwait(
            false);
        return json is null ? null : ParseChannel(json.Value);
    }

    public async Task<Stream?> OpenCacheFileAsync(string cachePath, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(cachePath);
        if (!TaFormats.IsCachePath(cachePath))
        {
            throw new ArgumentException(
                "Only TubeArchivist cache image paths (/cache/videos/…, /cache/channels/…) can be fetched.",
                nameof(cachePath));
        }

        using HttpResponseMessage response = await SendAsync(cachePath.TrimStart('/'), cancellationToken)
            .ConfigureAwait(false);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        await ThrowIfFailedAsync(response, cachePath, cancellationToken).ConfigureAwait(false);

        // Images are small: buffer so the caller doesn't hold the connection.
        MemoryStream buffer = new();
        await response.Content.CopyToAsync(buffer, cancellationToken).ConfigureAwait(false);
        buffer.Position = 0;
        return buffer;
    }

    internal static string BuildVideoListUrl(VideoPageQuery query, int page)
    {
        List<string> parts = new()
        {
            "sort=" + query.Sort.ToString().ToLowerInvariant(),
            "order=" + query.Order.ToString().ToLowerInvariant(),
        };
        if (query.ChannelId is not null)
        {
            EnsureFormat(query.ChannelId, TaFormats.ChannelId(), nameof(query.ChannelId));
            parts.Add("channel=" + query.ChannelId);
        }

        if (query.Type is not null)
        {
            parts.Add("type=" + TaFormats.VideoTypes.Single(kv => kv.Value == query.Type.Value).Key);
        }

        parts.Add("page=" + page.ToString(CultureInfo.InvariantCulture));
        return "api/video/?" + string.Join('&', parts);
    }

    private Video ParseVideo(JsonElement json)
    {
        VideoDto dto = Deserialize<VideoDto>(json);
        videoValidator.Validate(dto);
        return TaMapper.ToVideo(dto);
    }

    private Channel ParseChannel(JsonElement json)
    {
        ChannelDto dto = Deserialize<ChannelDto>(json);
        channelValidator.Validate(dto);
        return TaMapper.ToChannel(dto);
    }

    private TaChannel ParseListedChannel(JsonElement json)
    {
        ChannelDto dto = Deserialize<ChannelDto>(json);
        channelValidator.Validate(dto);
        return new TaChannel(TaMapper.ToChannel(dto), dto.ChannelSubscribed == true);
    }

    private async Task<Page<T>> GetPageAsync<T>(
        string url,
        int page,
        Func<JsonElement, T> parse,
        CancellationToken cancellationToken)
    {
        PagedDto? paged = await GetJsonAsync<PagedDto>(url, cancellationToken).ConfigureAwait(false);
        if (paged?.Data is null || paged.Paginate?.PageSize is not > 0)
        {
            throw new TubeArchivistProtocolException(
                $"/{url}: expected a paginated answer with data and paginate.page_size.");
        }

        List<T> items = new(paged.Data.Count);
        List<RejectedItem> rejected = new();
        foreach (JsonElement element in paged.Data)
        {
            // One broken document must not hide the rest of the page.
            try
            {
                items.Add(parse(element));
            }
            catch (ValidationException ex)
            {
                rejected.Add(new RejectedItem(TryGetId(element), ex.Subject, ex.Errors));
            }
        }

        return new Page<T>(
            page,
            paged.Paginate.PageSize.Value,
            items,
            paged.Paginate.TotalHits ?? 0,
            paged.Paginate.MaxHits ?? false)
        {
            Rejected = rejected,
        };
    }

    private async Task<T?> GetJsonAsync<T>(string url, CancellationToken cancellationToken)
    {
        JsonElement json = await GetJsonOrNullAsync(url, cancellationToken).ConfigureAwait(false)
            ?? throw new TubeArchivistProtocolException(
                $"/{url}: TubeArchivist answered 404.",
                HttpStatusCode.NotFound);
        return Deserialize<T>(json);
    }

    private async Task<JsonElement?> GetJsonOrNullAsync(string url, CancellationToken cancellationToken)
    {
        using HttpResponseMessage response = await SendAsync(url, cancellationToken).ConfigureAwait(false);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        await ThrowIfFailedAsync(response, url, cancellationToken).ConfigureAwait(false);
        try
        {
            await using Stream body = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            using JsonDocument document = await JsonDocument.ParseAsync(body, cancellationToken: cancellationToken)
                .ConfigureAwait(false);
            return document.RootElement.Clone();
        }
        catch (JsonException ex)
        {
            throw new TubeArchivistProtocolException($"/{url}: answer is not JSON.", response.StatusCode, ex);
        }
    }

    private async Task<HttpResponseMessage> SendAsync(string url, CancellationToken cancellationToken)
    {
        try
        {
            return await http
                .GetAsync(
                    new Uri(url, UriKind.Relative),
                    HttpCompletionOption.ResponseHeadersRead,
                    cancellationToken)
                .ConfigureAwait(false);
        }
        catch (HttpRequestException ex)
        {
            throw new TubeArchivistUnavailableException(
                $"/{url}: TubeArchivist can't be reached ({ex.HttpRequestError}).",
                ex);
        }
        catch (TimeoutRejectedException ex)
        {
            throw new TubeArchivistUnavailableException($"/{url}: TubeArchivist timed out.", ex);
        }
        catch (BrokenCircuitException ex)
        {
            // After many failures in a row the resilience pipeline stops calling for a while: a run must fail like
            // for any unreachable TubeArchivist, not with an exception nothing expects (it left runs "running").
            throw new TubeArchivistUnavailableException(
                $"/{url}: TubeArchivist failed too often just now; not called for a few seconds.",
                ex);
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            throw new TubeArchivistUnavailableException($"/{url}: TubeArchivist timed out.", ex);
        }
    }

    private static async Task ThrowIfFailedAsync(
        HttpResponseMessage response,
        string url,
        CancellationToken cancellationToken)
    {
        HttpStatusCode status = response.StatusCode;
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        string where = "/" + url.TrimStart('/');
        switch (status)
        {
            case HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden:
                throw new TubeArchivistAuthException(
                    $"{where}: TubeArchivist refused the API token ({(int)status}).",
                    status);
            case HttpStatusCode.BadRequest:
                IReadOnlyDictionary<string, IReadOnlyList<string>> errors =
                    await ReadFieldErrorsAsync(response, cancellationToken).ConfigureAwait(false);
                throw new TubeArchivistBadRequestException(
                    $"{where}: TubeArchivist rejected the request: "
                        + string.Join("; ", errors.Select(e => $"{e.Key}: {string.Join(' ', e.Value)}")),
                    errors);
            case >= HttpStatusCode.InternalServerError:
                throw new TubeArchivistServerException(
                    $"{where}: TubeArchivist failed ({(int)status}). Past its 10,000-result window, TA answers 500.",
                    status);
            default:
                throw new TubeArchivistException($"{where}: unexpected status {(int)status}.", status);
        }
    }

    private static async Task<IReadOnlyDictionary<string, IReadOnlyList<string>>> ReadFieldErrorsAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        try
        {
            Dictionary<string, List<string>>? raw = await response.Content
                .ReadFromJsonAsync<Dictionary<string, List<string>>>(cancellationToken)
                .ConfigureAwait(false);
            return raw?.ToDictionary(kv => kv.Key, kv => (IReadOnlyList<string>)kv.Value) ?? [];
        }
        catch (JsonException)
        {
            return new Dictionary<string, IReadOnlyList<string>>();
        }
    }

    private static T Deserialize<T>(JsonElement json)
    {
        try
        {
            return json.Deserialize<T>(TaJson.Options) ?? throw new JsonException("null document");
        }
        catch (JsonException ex)
        {
            // A field with the wrong JSON type: report it like any other broken document.
            string kind = typeof(T).Name.Replace("Dto", "", StringComparison.Ordinal).ToLowerInvariant();
            throw new ValidationException(
                $"TubeArchivist {kind} {TryGetId(json) ?? "(no valid id)"}",
                [new ValidationError(ex.Path ?? "$", "has an unexpected JSON type.")]);
        }
    }

    private static string? TryGetId(JsonElement json)
    {
        foreach (string name in (string[])["youtube_id", "channel_id"])
        {
            if (json.ValueKind == JsonValueKind.Object && json.TryGetProperty(name, out JsonElement id)
                && id.ValueKind == JsonValueKind.String
                && id.GetString() is { } s
                && (TaFormats.VideoId().IsMatch(s) || TaFormats.ChannelId().IsMatch(s)))
            {
                return s;
            }
        }

        return null;
    }

    private static void EnsurePageNumber(int page)
    {
        // Page 0 is an alias of page 1 in TA, and negative pages make it fail with a 500.
        ArgumentOutOfRangeException.ThrowIfLessThan(page, 1);
    }

    private static void EnsureFormat(string value, System.Text.RegularExpressions.Regex format, string name)
    {
        ArgumentNullException.ThrowIfNull(value, name);
        if (!format.IsMatch(value))
        {
            throw new ArgumentException($"'{value}' is not a valid id.", name);
        }
    }
}
