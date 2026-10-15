using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using ManuHub.Ytdlp.NET;
using ManuHub.Ytdlp.NET.Core;
using Microsoft.Extensions.Logging;
using TonTuyau.Application.Configuration;
using TonTuyau.Application.YouTube;
using TonTuyau.YouTube.Dtos;

namespace TonTuyau.YouTube;

/// <summary>
/// <see cref="IYouTubeClient"/> over the yt-dlp program, run through Ytdlp.NET (one run per question). The JSON yt-dlp
/// prints is read here, and its error lines too (<see cref="YtdlpLog"/>): Ytdlp.NET's typed metadata gives no reason
/// when it fails, and the reason is what tells "gone" from "try again" (<see cref="YtdlpErrors"/>).
/// </summary>
internal sealed partial class YouTubeClient(
    TonTuyauOptions options,
    YtdlpLocator ytdlp,
    IYouTubeCookieSource cookies,
    ILogger<YouTubeClient> logger) : IYouTubeClient
{
    /// <summary>The longest one question may take; past it, the check failed (yt-dlp is stopped).</summary>
    public static readonly TimeSpan CallTimeout = TimeSpan.FromMinutes(2);

    /// <summary>The same with comments: YouTube gives them a page at a time, a popular video takes minutes.</summary>
    public static readonly TimeSpan CommentsTimeout = TimeSpan.FromMinutes(10);

    /// <summary>
    /// Every run: no user config file, no cache (the image's home isn't writable), plain text, nothing downloaded.
    /// </summary>
    private const string CommonArguments = "--ignore-config --no-cache-dir --color never --skip-download";

    private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = false };

    public async Task<YouTubeLookup<YouTubeVideo>> GetVideoAsync(
        string videoId,
        YouTubeCommentsRequest? comments,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(videoId);
        if (comments is not null)
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(comments.Max, 1);
        }

        string arguments = $"{CommonArguments} --dump-single-json --no-playlist {CommentArguments(comments)}"
            + $"https://www.youtube.com/watch?v={videoId}";
        TimeSpan limit = comments is null ? CallTimeout : CommentsTimeout;
        YtdlpRun run = await RunAsync(arguments, limit, cancellationToken).ConfigureAwait(false);
        if (run.Failure is not null)
        {
            return new YouTubeLookup<YouTubeVideo>(run.Failure.Value.Answer, null, run.Failure.Value.Reason);
        }

        VideoInfoDto? info = Parse<VideoInfoDto>(run.Output, videoId);
        if (info is null || info.Id != videoId || string.IsNullOrWhiteSpace(info.Title) || info.ChannelId is null)
        {
            LogUnexpectedAnswer(videoId);
            return new YouTubeLookup<YouTubeVideo>(
                YouTubeAnswer.CheckFailed,
                null,
                "yt-dlp's answer isn't this video's metadata.");
        }

        return new YouTubeLookup<YouTubeVideo>(YouTubeAnswer.Found, VideoOf(info, comments is not null), null);
    }

    public async Task<YouTubeLookup<YouTubeChannel>> GetChannelAsync(
        string channelId,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(channelId);
        // The channel's page as a playlist, without a single video of it: only the channel's own fields.
        string arguments = $"{CommonArguments} --dump-single-json --flat-playlist --playlist-items 0 "
            + $"https://www.youtube.com/channel/{channelId}";
        YtdlpRun run = await RunAsync(arguments, CallTimeout, cancellationToken).ConfigureAwait(false);
        if (run.Failure is not null)
        {
            return new YouTubeLookup<YouTubeChannel>(run.Failure.Value.Answer, null, run.Failure.Value.Reason);
        }

        ChannelInfoDto? info = Parse<ChannelInfoDto>(run.Output, channelId);
        if (info is null || info.ChannelId != channelId || string.IsNullOrWhiteSpace(info.Channel))
        {
            LogUnexpectedAnswer(channelId);
            return new YouTubeLookup<YouTubeChannel>(
                YouTubeAnswer.CheckFailed,
                null,
                "yt-dlp's answer isn't this channel's metadata.");
        }

        return new YouTubeLookup<YouTubeChannel>(YouTubeAnswer.Found, ChannelOf(info), null);
    }

    public async Task<YouTubeTools> GetToolsAsync(CancellationToken cancellationToken)
    {
        string? running = null;
        if (Start(new YtdlpLog(logger)) is { } program)
        {
            try
            {
                string version = await program
                    .VersionAsync(cancellationToken)
                    .ConfigureAwait(false);
                running = string.IsNullOrWhiteSpace(version) ? null : version.Trim();
            }
            catch (Exception ex) when (IsMissingProgram(ex))
            {
                LogToolMissing(ytdlp.Path, ex.Message);
            }
        }

        return new YouTubeTools(running, await DenoVersionAsync(cancellationToken).ConfigureAwait(false));
    }

    public async Task<YouTubeCookieCheck> CheckCookieAsync(CancellationToken cancellationToken)
    {
        if (await cookies.GetAsync(cancellationToken).ConfigureAwait(false) is null)
        {
            return new YouTubeCookieCheck(YouTubeCookieState.Unchecked, "There's no YouTube cookie.");
        }

        // The account's Liked videos ("LL"): only a signed-in session sees them. One entry is enough.
        string arguments = $"{CommonArguments} --dump-single-json --flat-playlist --playlist-items 1 "
            + "https://www.youtube.com/playlist?list=LL";
        YtdlpRun run = await RunAsync(arguments, CallTimeout, cancellationToken).ConfigureAwait(false);
        if (run.Failure is null)
        {
            return new YouTubeCookieCheck(YouTubeCookieState.Works, null);
        }

        // Signed out, expired, not a session: refused. A bot check or the network: nothing to conclude.
        (YouTubeAnswer answer, string reason) = run.Failure.Value;
        return answer == YouTubeAnswer.CheckFailed && (YtdlpErrors.DidntAnswer(reason) || run.Silent)
            ? new YouTubeCookieCheck(YouTubeCookieState.Unchecked, reason)
            : new YouTubeCookieCheck(YouTubeCookieState.Refused, reason);
    }

    /// <param name="withComments">Whether comments were asked for: then a video without any has an empty list.</param>
    internal static YouTubeVideo VideoOf(VideoInfoDto info, bool withComments) => new(
        info.Id!,
        info.Title!,
        info.Description ?? "",
        info.ChannelId!,
        info.Channel ?? "",
        PublishedOf(info),
        info.Duration is { } seconds ? (int)Math.Round(seconds) : null,
        info.ViewCount,
        info.LikeCount,
        info.Tags ?? [],
        info.Categories ?? [],
        ThumbnailsOf(info.Thumbnails),
        info.LiveStatus,
        info.CommentCount,
        withComments ? CommentsOf(info.Comments) : null);

    internal static YouTubeChannel ChannelOf(ChannelInfoDto info) => new(
        info.ChannelId!,
        info.Channel!,
        info.Description ?? "",
        info.UploaderId,
        info.ChannelFollowerCount,
        info.Tags ?? [],
        ThumbnailsOf(info.Thumbnails));

    /// <summary>
    /// yt-dlp's <c>--write-comments</c> and its YouTube setting: <c>max_comments</c> counts answers too, and every
    /// thread's answers are read (<c>all</c>) within that total. YouTube's order: top comments first (another order
    /// would take a <c>;</c>, which Ytdlp.NET refuses in an argument).
    /// </summary>
    private static string CommentArguments(YouTubeCommentsRequest? comments) =>
        comments is null
            ? ""
            : string.Create(
                CultureInfo.InvariantCulture,
                $"--write-comments --extractor-args youtube:max_comments={comments.Max},all,all,all ");

    private static List<YouTubeComment> CommentsOf(IReadOnlyList<CommentDto>? comments) =>
        [
            .. (comments ?? [])
                .Where(c => !string.IsNullOrEmpty(c.Id))
                .Select(c => new YouTubeComment(
                    c.Id!,
                    c.Parent is null or "root" ? null : c.Parent,
                    c.Text ?? "",
                    c.Author ?? "",
                    c.AuthorId,
                    c.AuthorThumbnail,
                    c.Timestamp is { } seconds ? DateTimeOffset.FromUnixTimeSeconds(seconds) : null,
                    c.LikeCount,
                    c.IsPinned == true,
                    c.AuthorIsUploader == true,
                    c.IsFavorited == true)),
        ];

    /// <summary>To the second (<c>timestamp</c>) when YouTube says, else the day (<c>upload_date</c>), else unknown.</summary>
    private static DateTimeOffset? PublishedOf(VideoInfoDto info)
    {
        if (info.Timestamp is { } seconds)
        {
            return DateTimeOffset.FromUnixTimeSeconds(seconds);
        }

        return DateTime.TryParseExact(
            info.UploadDate,
            "yyyyMMdd",
            CultureInfo.InvariantCulture,
            DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal,
            out DateTime day)
            ? new DateTimeOffset(day, TimeSpan.Zero)
            : null;
    }

    private static List<YouTubeThumbnail> ThumbnailsOf(IReadOnlyList<ThumbnailDto>? thumbnails) =>
        [
            .. (thumbnails ?? [])
                .Where(t => !string.IsNullOrEmpty(t.Url))
                .Select(t => new YouTubeThumbnail(t.Url!, t.Width, t.Height)),
        ];

    /// <summary>
    /// One run of yt-dlp: its output, or why it failed. A new Ytdlp.NET instance and log each time, so the error lines
    /// are this run's only. Signed in with the cookie when there's one: a file of this run's, readable by the backend
    /// only, deleted at the end.
    /// </summary>
    private async Task<YtdlpRun> RunAsync(string arguments, TimeSpan limit, CancellationToken cancellationToken)
    {
        string? cookie = await cookies.GetAsync(cancellationToken).ConfigureAwait(false);
        if (cookie is null)
        {
            return await RunOnceAsync(arguments, limit, cancellationToken).ConfigureAwait(false);
        }

        string file = Path.Combine(Path.GetTempPath(), $"tontuyau-cookies-{Guid.NewGuid():N}.txt");
        try
        {
            await WriteCookieAsync(file, cookie, cancellationToken).ConfigureAwait(false);
            return await RunOnceAsync($"--cookies {file} {arguments}", limit, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            File.Delete(file);
        }
    }

    /// <summary>Created readable and writable by its owner only, before the content is written.</summary>
    private static async Task WriteCookieAsync(string file, string cookie, CancellationToken cancellationToken)
    {
        FileStreamOptions privately = new() { Mode = FileMode.CreateNew, Access = FileAccess.Write };
        if (!OperatingSystem.IsWindows())
        {
            privately.UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;
        }

        FileStream stream = new(file, privately);
        await using (stream.ConfigureAwait(false))
        {
            StreamWriter writer = new(stream);
            await using (writer.ConfigureAwait(false))
            {
                await writer
                    .WriteAsync(cookie.AsMemory(), cancellationToken)
                    .ConfigureAwait(false);
            }
        }
    }

    private async Task<YtdlpRun> RunOnceAsync(string arguments, TimeSpan limit, CancellationToken cancellationToken)
    {
        YtdlpLog log = new(logger);
        if (Start(log) is not { } program)
        {
            return YtdlpRun.Failed(YouTubeAnswer.CheckFailed, "yt-dlp can't be run here.");
        }

        using CancellationTokenSource timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(limit);
        ProcessResult result;
        try
        {
            result = await program
                .ExecuteRawAsync(arguments, onLineReceived: null, timeout.Token)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            LogTimedOut(limit);
            return YtdlpRun.Failed(YouTubeAnswer.CheckFailed, "yt-dlp took too long.");
        }
        catch (Exception ex) when (IsMissingProgram(ex))
        {
            LogToolMissing(ytdlp.Path, ex.Message);
            return YtdlpRun.Failed(YouTubeAnswer.CheckFailed, "yt-dlp can't be run here.");
        }

        cancellationToken.ThrowIfCancellationRequested();
        if (result.IsSuccess)
        {
            return new YtdlpRun(result.FullOutput ?? "", null);
        }

        (YouTubeAnswer answer, string reason) = YtdlpErrors.Classify(log.Errors, result.ExitCode);
        LogRefused(answer, reason);
        return YtdlpRun.Failed(answer, reason) with { Silent = YtdlpErrors.ReasonOf(log.Errors).Length == 0 };
    }

    /// <summary>Ytdlp.NET over the configured program, or null (logged) when it's not there or can't be run.</summary>
    private Ytdlp? Start(YtdlpLog log)
    {
        try
        {
            return new Ytdlp(ytdlp.Path, log);
        }
        catch (Exception ex) when (IsMissingProgram(ex))
        {
            LogToolMissing(ytdlp.Path, ex.Message);
            return null;
        }
    }

    /// <summary>The program isn't there or can't be started (Ytdlp.NET checks it when it starts the first run).</summary>
    private static bool IsMissingProgram(Exception ex) =>
        ex is YtdlpException or FileNotFoundException or UnauthorizedAccessException or Win32Exception;

    private T? Parse<T>(string output, string id)
        where T : class
    {
        try
        {
            return JsonSerializer.Deserialize<T>(output, Json);
        }
        catch (JsonException ex)
        {
            LogNotJson(id, ex.Message);
            return null;
        }
    }

    /// <summary>"deno 2.9.7 (stable, release, x86_64-unknown-linux-gnu)" → "2.9.7"; null when it can't be run.</summary>
    private async Task<string?> DenoVersionAsync(CancellationToken cancellationToken)
    {
        ProcessStartInfo start = new(options.YouTube.DenoPath, "--version")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        try
        {
            using Process? deno = Process.Start(start);
            if (deno is null)
            {
                return null;
            }

            string output = await deno.StandardOutput
                .ReadToEndAsync(cancellationToken)
                .ConfigureAwait(false);
            await deno
                .WaitForExitAsync(cancellationToken)
                .ConfigureAwait(false);
            string[] first = output
                .Split('\n', 2)[0]
                .Split(' ', StringSplitOptions.RemoveEmptyEntries);
            return first.Length > 1 && first[0] == "deno" ? first[1] : null;
        }
        catch (Win32Exception ex)
        {
            LogToolMissing(options.YouTube.DenoPath, ex.Message);
            return null;
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "{Program} can't be run: {Error}")]
    private partial void LogToolMissing(string program, string error);

    [LoggerMessage(Level = LogLevel.Warning, Message = "yt-dlp took more than {Timeout} and was stopped.")]
    private partial void LogTimedOut(TimeSpan timeout);

    [LoggerMessage(Level = LogLevel.Information, Message = "YouTube: {Answer}, {Reason}")]
    private partial void LogRefused(YouTubeAnswer answer, string reason);

    [LoggerMessage(Level = LogLevel.Warning, Message = "yt-dlp's answer about {Id} isn't JSON: {Error}")]
    private partial void LogNotJson(string id, string error);

    [LoggerMessage(Level = LogLevel.Warning, Message = "yt-dlp answered, but not with {Id}'s metadata.")]
    private partial void LogUnexpectedAnswer(string id);

    /// <summary>A run's output, or why it failed; <see cref="Silent"/>: it failed without an error line.</summary>
    private readonly record struct YtdlpRun(string Output, (YouTubeAnswer Answer, string Reason)? Failure)
    {
        public bool Silent { get; init; }

        public static YtdlpRun Failed(YouTubeAnswer answer, string reason) => new("", (answer, reason));
    }
}
