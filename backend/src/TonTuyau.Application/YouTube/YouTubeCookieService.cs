using Microsoft.Extensions.Logging;
using TonTuyau.Application.Browse;
using TonTuyau.Application.Validation;

namespace TonTuyau.Application.YouTube;

/// <summary>
/// The YouTube cookie's rules (1.5): one cookie, the last sent; only its <c>youtube.com</c> lines kept; checked against
/// YouTube when it comes and when asked; never shown, never logged. The database calls are
/// <see cref="IYouTubeCookieRepository"/>'s, the check <see cref="IYouTubeClient"/>'s.
/// </summary>
public sealed partial class YouTubeCookieService(
    IYouTubeCookieRepository cookies,
    IYouTubeClient youTube,
    TimeProvider clock,
    ILogger<YouTubeCookieService> logger)
{
    public async Task<YouTubeCookieStatus?> GetStatusAsync(CancellationToken cancellationToken)
    {
        YouTubeCookie? cookie = await cookies
            .GetAsync(cancellationToken)
            .ConfigureAwait(false);
        return cookie is null ? null : (YouTubeCookieStatus)cookie;
    }

    public async Task<YouTubeOverview> GetOverviewAsync(CancellationToken cancellationToken)
    {
        YouTubeCookieStatus? cookie = await GetStatusAsync(cancellationToken).ConfigureAwait(false);
        YouTubeTools tools = await youTube
            .GetToolsAsync(cancellationToken)
            .ConfigureAwait(false);
        return new YouTubeOverview(cookie, tools);
    }

    /// <summary>Keeps the cookie sent (its <c>youtube.com</c> lines), then checks it.</summary>
    /// <exception cref="ValidationException">Empty, too long, or without a <c>youtube.com</c> cookie.</exception>
    public async Task<YouTubeCookieStatus> SetAsync(string? text, string by, CancellationToken cancellationToken)
    {
        string content = NetscapeCookies.KeepYouTube(text);
        await cookies
            .SaveAsync(content, clock.GetUtcNow(), by, cancellationToken)
            .ConfigureAwait(false);
        LogReceived(by);
        return await CheckAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Tries the cookie stored against YouTube, and notes what it said.</summary>
    /// <exception cref="NotFoundException">There's no cookie.</exception>
    public async Task<YouTubeCookieStatus> CheckAsync(CancellationToken cancellationToken)
    {
        if (await cookies.GetAsync(cancellationToken).ConfigureAwait(false) is null)
        {
            throw NoCookie();
        }

        YouTubeCookieCheck check = await youTube
            .CheckCookieAsync(cancellationToken)
            .ConfigureAwait(false);
        await cookies
            .SetCheckAsync(check.State, clock.GetUtcNow(), check.Reason, cancellationToken)
            .ConfigureAwait(false);
        LogChecked(check.State, check.Reason ?? "");
        return await GetStatusAsync(cancellationToken).ConfigureAwait(false) ?? throw NoCookie();
    }

    /// <exception cref="NotFoundException">There's no cookie.</exception>
    public async Task DeleteAsync(string by, CancellationToken cancellationToken)
    {
        if (!await cookies.DeleteAsync(cancellationToken).ConfigureAwait(false))
        {
            throw NoCookie();
        }

        LogDeleted(by);
    }

    private static NotFoundException NoCookie() => new("There's no YouTube cookie.");

    [LoggerMessage(Level = LogLevel.Information, Message = "YouTube cookie received from {By}.")]
    private partial void LogReceived(string by);

    [LoggerMessage(Level = LogLevel.Information, Message = "YouTube cookie checked: {State}. {Reason}")]
    private partial void LogChecked(YouTubeCookieState state, string reason);

    [LoggerMessage(Level = LogLevel.Information, Message = "YouTube cookie removed by {By}.")]
    private partial void LogDeleted(string by);
}
