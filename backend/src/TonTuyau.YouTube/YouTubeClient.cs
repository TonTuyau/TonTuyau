using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using ManuHub.Ytdlp.NET;
using ManuHub.Ytdlp.NET.Core;
using Microsoft.Extensions.Logging;
using TonTuyau.Application.Configuration;
using TonTuyau.Application.Library;
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
    YtdlpTempFolders temp,
    ILogger<YouTubeClient> logger) : IYouTubeClient
{
    /// <summary>The longest one question may take; past it, the check failed (yt-dlp is stopped).</summary>
    public static readonly TimeSpan CallTimeout = TimeSpan.FromMinutes(2);

    /// <summary>The same with comments: YouTube gives them a page at a time, a popular video takes minutes.</summary>
    public static readonly TimeSpan CommentsTimeout = TimeSpan.FromMinutes(10);

    /// <summary>
    /// For more than <see cref="QuickComments"/> comments, or all of them: YouTube gives about 20 a request, so a video
    /// with many takes long (the run can still be canceled).
    /// </summary>
    public static readonly TimeSpan ManyCommentsTimeout = TimeSpan.FromHours(6);

    /// <summary>Up to this many comments, <see cref="CommentsTimeout"/> is enough.</summary>
    public const int QuickComments = 500;

    /// <summary>The longest a download may take (2.0): hours of video, on a slow connection.</summary>
    public static readonly TimeSpan DownloadTimeout = TimeSpan.FromHours(12);

    /// <summary>The longest a tab's listing may take (2.0, B5): a whole tab of thousands, a page of 30 at a time.</summary>
    public static readonly TimeSpan ListTimeout = TimeSpan.FromMinutes(30);

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
        if (comments?.Max is not null)
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(comments.Max.Value, 1);
        }

        string arguments = $"{CommonArguments} --dump-single-json --no-playlist {CommentArguments(comments)}"
            + $"https://www.youtube.com/watch?v={videoId}";
        TimeSpan limit = comments switch
        {
            null => CallTimeout,
            { Max: <= QuickComments } => CommentsTimeout,
            _ => ManyCommentsTimeout,
        };
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
        string channel,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(channel);
        bool handle = LibraryIds.IsHandle(channel);
        // The channel's page as a playlist, without a single video of it: only the channel's own fields.
        string arguments = $"{CommonArguments} --dump-single-json --flat-playlist --playlist-items 0 "
            + (handle ? $"https://www.youtube.com/{channel}" : $"https://www.youtube.com/channel/{channel}");
        YtdlpRun run = await RunAsync(arguments, CallTimeout, cancellationToken).ConfigureAwait(false);
        if (run.Failure is not null)
        {
            return new YouTubeLookup<YouTubeChannel>(run.Failure.Value.Answer, null, run.Failure.Value.Reason);
        }

        ChannelInfoDto? info = Parse<ChannelInfoDto>(run.Output, channel);
        // A handle's answer gives the channel's id; an id's must be that one.
        bool itsId = handle ? LibraryIds.IsChannelId(info?.ChannelId) : info?.ChannelId == channel;
        if (info is null || !itsId || string.IsNullOrWhiteSpace(info.Channel))
        {
            LogUnexpectedAnswer(channel);
            return new YouTubeLookup<YouTubeChannel>(
                YouTubeAnswer.CheckFailed,
                null,
                "yt-dlp's answer isn't this channel's metadata.");
        }

        return new YouTubeLookup<YouTubeChannel>(YouTubeAnswer.Found, ChannelOf(info), null);
    }

    /// <remarks>
    /// yt-dlp is started directly here, with its arguments one by one: the format is the admin's, in yt-dlp's syntax,
    /// which Ytdlp.NET's own check refuses (it takes the <c>&gt;</c> of <c>height&gt;=720</c> for a shell redirection).
    /// </remarks>
    public async Task<YouTubeLookup<YouTubeDownload>> DownloadVideoAsync(
        string videoId,
        string format,
        string folder,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(videoId);
        ArgumentException.ThrowIfNullOrWhiteSpace(format);
        ArgumentException.ThrowIfNullOrEmpty(folder);
        List<string> arguments =
        [
            "--ignore-config", "--no-cache-dir", "--color", "never", "--no-playlist", "--no-progress", "--no-mtime",
            "--format", format,
            // One .mkv: the video and audio merged into it, or the one file remuxed; chapters and metadata inside.
            "--merge-output-format", "mkv", "--remux-video", "mkv", "--embed-chapters", "--embed-metadata",
            "--paths", $"home:{folder}", "--paths", $"temp:{folder}", "--output", "%(id)s.%(ext)s",
            // What it downloaded, as the info dict yt-dlp writes next to it: the metadata read with the download.
            "--write-info-json", "--no-write-playlist-metafiles",
            $"https://www.youtube.com/watch?v={videoId}",
        ];
        // A name (the default, "ffmpeg") is found on the PATH by yt-dlp itself, ffprobe with it; a path is passed on.
        if (options.YouTube.FfmpegPath.Contains(Path.DirectorySeparatorChar, StringComparison.Ordinal))
        {
            arguments.InsertRange(arguments.Count - 1, ["--ffmpeg-location", options.YouTube.FfmpegPath]);
        }

        YtdlpRun run = await RunDirectAsync(arguments, DownloadTimeout, cancellationToken).ConfigureAwait(false);
        if (run.Failure is not null)
        {
            return new YouTubeLookup<YouTubeDownload>(run.Failure.Value.Answer, null, run.Failure.Value.Reason);
        }

        string infoFile = Path.Combine(folder, $"{videoId}.info.json");
        string file = Path.Combine(folder, $"{videoId}.mkv");
        VideoInfoDto? info = File.Exists(infoFile)
            ? Parse<VideoInfoDto>(
                await File.ReadAllTextAsync(infoFile, cancellationToken).ConfigureAwait(false),
                videoId)
            : null;
        if (info is null
            || !File.Exists(file)
            || info.Id != videoId
            || string.IsNullOrWhiteSpace(info.Title)
            || info.ChannelId is null)
        {
            LogNoDownload(videoId);
            return new YouTubeLookup<YouTubeDownload>(
                YouTubeAnswer.CheckFailed,
                null,
                "yt-dlp didn't leave the video's file and metadata.");
        }

        YouTubeDownload download = new(
            VideoOf(info, withComments: false),
            file,
            new FileInfo(file).Length,
            TypeOf(info),
            TracksOf(info));
        return new YouTubeLookup<YouTubeDownload>(YouTubeAnswer.Found, download, null);
    }

    /// <remarks>
    /// yt-dlp lists lazily (<c>--lazy-playlist</c>), a line per entry as YouTube pages them: once
    /// <paramref name="stopAt"/> says so, yt-dlp is stopped, so the pages after it are never asked for.
    /// </remarks>
    public async Task<YouTubeLookup<IReadOnlyList<YouTubeListed>>> ListTabAsync(
        string channelId,
        ChannelTab tab,
        Func<YouTubeListed, bool> stopAt,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(channelId);
        ArgumentNullException.ThrowIfNull(stopAt);
        string page = tab.ToString().ToLowerInvariant();
        List<YouTubeListed> listed = [];
        bool stopped = false;
        YtdlpRun run = await RunDirectAsync(
            [
                "--ignore-config",
                "--no-cache-dir",
                "--color",
                "never",
                "--flat-playlist",
                "--lazy-playlist",
                "--print",
                "%(.{id,live_status,title})j",
                $"https://www.youtube.com/channel/{channelId}/{page}",
            ],
            ListTimeout,
            cancellationToken,
            line =>
            {
                if (stopped || ListedOf(line) is not { } entry)
                {
                    return stopped;
                }

                listed.Add(entry);
                stopped = stopAt(entry);
                return stopped;
            })
            .ConfigureAwait(false);
        if (run.Failure is not null)
        {
            // A channel without this tab (no shorts, never streamed): nothing to list.
            return run.Failure.Value.Reason.Contains($"does not have a {page} tab", StringComparison.OrdinalIgnoreCase)
                ? new YouTubeLookup<IReadOnlyList<YouTubeListed>>(YouTubeAnswer.Found, [], null)
                : new YouTubeLookup<IReadOnlyList<YouTubeListed>>(
                    run.Failure.Value.Answer,
                    null,
                    run.Failure.Value.Reason);
        }

        return new YouTubeLookup<IReadOnlyList<YouTubeListed>>(YouTubeAnswer.Found, listed, null);
    }

    /// <summary>A listing's line: an entry with a video id; null otherwise (a warning, a playlist's own line).</summary>
    internal static YouTubeListed? ListedOf(string line)
    {
        if (!line.StartsWith('{'))
        {
            return null;
        }

        try
        {
            ListedDto? dto = JsonSerializer.Deserialize<ListedDto>(line, Json);
            return LibraryIds.IsVideoId(dto?.Id)
                ? new YouTubeListed(
                    dto!.Id!,
                    dto.LiveStatus is "is_upcoming" or "is_live",
                    string.IsNullOrWhiteSpace(dto.Title) ? null : dto.Title)
                : null;
        }
        catch (JsonException)
        {
            return null;
        }
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
        return answer == YouTubeAnswer.Refused
            || answer == YouTubeAnswer.CheckFailed && (YtdlpErrors.DidntAnswer(reason) || run.Silent)
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
        info.Duration is null ? null : (int)Math.Round(info.Duration.Value),
        info.ViewCount,
        info.LikeCount,
        info.Tags ?? [],
        info.Categories ?? [],
        ThumbnailsOf(info.Thumbnails),
        info.LiveStatus,
        info.CommentCount,
        withComments ? CommentsOf(info.Comments) : null,
        ChaptersOf(info.Chapters),
        info.Availability);

    /// <summary>A stream when it was (or is) live, a short when YouTube says so, a video otherwise.</summary>
    internal static VideoType TypeOf(VideoInfoDto info) =>
        info.LiveStatus is "was_live" or "is_live" or "post_live" || info.MediaType == "livestream"
            ? VideoType.Stream
            : info.MediaType == "short"
                ? VideoType.Short
                : VideoType.Video;

    /// <summary>The downloaded formats' tracks: each format's video and its audio, those it has.</summary>
    internal static List<MediaTrack> TracksOf(VideoInfoDto info)
    {
        IReadOnlyList<FormatDto> formats = info.RequestedFormats is { Count: > 0 } requested
            ? requested
            : [new FormatDto(info.Vcodec, info.Acodec, info.Width, info.Height, info.Tbr)];
        List<MediaTrack> tracks = [];
        foreach (FormatDto format in formats)
        {
            long bitrate = format.Tbr is null ? 0 : (long)Math.Round(format.Tbr.Value * 1000);
            if (CodecName(format.Vcodec) is { } video)
            {
                tracks.Add(new MediaTrack(MediaTrackKind.Video, video, bitrate, format.Width, format.Height));
            }

            if (CodecName(format.Acodec) is { } audio)
            {
                tracks.Add(new MediaTrack(MediaTrackKind.Audio, audio, bitrate, null, null));
            }
        }

        return tracks;
    }

    /// <summary>
    /// YouTube's codec string as ffprobe names the codec (as TubeArchivist's tracks do): <c>vp09.00.40.08</c> →
    /// <c>vp9</c>, <c>av01…</c> → <c>av1</c>, <c>avc1…</c> → <c>h264</c>, <c>mp4a…</c> → <c>aac</c>; null for none.
    /// </summary>
    internal static string? CodecName(string? codec)
    {
        if (string.IsNullOrWhiteSpace(codec) || codec == "none")
        {
            return null;
        }

        string family = codec
            .Split('.')[0]
            .ToLowerInvariant();
        return family switch
        {
            "vp09" or "vp9" => "vp9",
            "vp08" or "vp8" => "vp8",
            "av01" or "av1" => "av1",
            "avc1" or "avc3" or "h264" => "h264",
            "hev1" or "hvc1" or "hevc" or "h265" => "hevc",
            "mp4a" or "aac" => "aac",
            "ec-3" or "eac3" => "eac3",
            "ac-3" or "ac3" => "ac3",
            _ => family,
        };
    }

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
            : "--write-comments --extractor-args youtube:max_comments="
                + (comments.Max?.ToString(CultureInfo.InvariantCulture) ?? "all")
                + ",all,all,all ";

    /// <returns>Null when yt-dlp gave none at all (it couldn't read them): not the same as a video without any.</returns>
    private static List<YouTubeComment>? CommentsOf(IReadOnlyList<CommentDto>? comments) =>
        comments is null
            ? null
            :
        [
            .. comments
                .Where(c => !string.IsNullOrEmpty(c.Id))
                .Select(c => new YouTubeComment(
                    c.Id!,
                    c.Parent is null or "root" ? null : c.Parent,
                    c.Text ?? "",
                    c.Author ?? "",
                    c.AuthorId,
                    c.AuthorThumbnail,
                    c.Timestamp is null ? null : DateTimeOffset.FromUnixTimeSeconds(c.Timestamp.Value),
                    c.LikeCount,
                    c.IsPinned == true,
                    c.AuthorIsUploader == true,
                    c.IsFavorited == true)),
        ];

    /// <summary>The chapters with a title and a start, in order; one without an end ends where it starts.</summary>
    private static List<YouTubeChapter> ChaptersOf(IReadOnlyList<ChapterDto>? chapters) =>
        [
            .. (chapters ?? [])
                .Where(c => c.StartTime is not null && !string.IsNullOrWhiteSpace(c.Title))
                .OrderBy(c => c.StartTime)
                .Select(c => new YouTubeChapter(c.StartTime!.Value, c.EndTime ?? c.StartTime.Value, c.Title!.Trim())),
        ];

    /// <summary>To the second (<c>timestamp</c>) when YouTube says, else the day (<c>upload_date</c>), else unknown.</summary>
    private static DateTimeOffset? PublishedOf(VideoInfoDto info)
    {
        if (info.Timestamp is not null)
        {
            return DateTimeOffset.FromUnixTimeSeconds(info.Timestamp.Value);
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
                .Select(t => new YouTubeThumbnail(t.Url!, t.Width, t.Height, t.Id)),
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

    /// <summary>
    /// One run of yt-dlp started directly (a download: its arguments one by one, no shell), signed in with the cookie
    /// when there's one, as <see cref="RunAsync"/>. Its error lines classified the same way.
    /// </summary>
    /// <param name="output">
    /// Given each line yt-dlp writes (a listing, B5); true: enough, yt-dlp is stopped and the run counts as done. Null:
    /// its output isn't read.
    /// </param>
    private async Task<YtdlpRun> RunDirectAsync(
        IReadOnlyList<string> arguments,
        TimeSpan limit,
        CancellationToken cancellationToken,
        Func<string, bool>? output = null)
    {
        string? cookie = await cookies.GetAsync(cancellationToken).ConfigureAwait(false);
        if (cookie is null)
        {
            return await RunProcessAsync(arguments, limit, output, cancellationToken).ConfigureAwait(false);
        }

        string file = Path.Combine(Path.GetTempPath(), $"tontuyau-cookies-{Guid.NewGuid():N}.txt");
        try
        {
            await WriteCookieAsync(file, cookie, cancellationToken).ConfigureAwait(false);
            return await RunProcessAsync(["--cookies", file, .. arguments], limit, output, cancellationToken)
                .ConfigureAwait(false);
        }
        finally
        {
            File.Delete(file);
        }
    }

    /// <summary>
    /// One run in a temp folder of its own (alpha.6: <see cref="YtdlpTempFolders"/>), removed once it's over, stopped
    /// or not: what yt-dlp unpacked there doesn't stay behind.
    /// </summary>
    private async Task<YtdlpRun> RunProcessAsync(
        IReadOnlyList<string> arguments,
        TimeSpan limit,
        Func<string, bool>? output,
        CancellationToken cancellationToken)
    {
        string folder = temp.NewFolder();
        try
        {
            return await RunProcessInAsync(arguments, limit, output, folder, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            temp.Remove(folder);
        }
    }

    private async Task<YtdlpRun> RunProcessInAsync(
        IReadOnlyList<string> arguments,
        TimeSpan limit,
        Func<string, bool>? output,
        string folder,
        CancellationToken cancellationToken)
    {
        ProcessStartInfo start = new(ytdlp.Path)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        start.Environment["TMPDIR"] = folder;
        foreach (string argument in arguments)
        {
            start.ArgumentList.Add(argument);
        }

        using Process process = new() { StartInfo = start };
        List<string> errors = [];
        process.ErrorDataReceived += (_, line) =>
        {
            if (line.Data is { Length: > 0 } text)
            {
                lock (errors)
                {
                    errors.Add(text);
                }

                LogDownloadLine(text);
            }
        };
        // Enough read (a listing): yt-dlp is stopped. Without a reader, its output is drained, so that yt-dlp never waits
        // on a full pipe.
        using CancellationTokenSource enough = new();
        process.OutputDataReceived += (_, line) =>
        {
            if (output is not null && line.Data is not null && !enough.IsCancellationRequested && output(line.Data))
            {
                enough.Cancel();
            }
        };
        try
        {
            process.Start();
        }
        catch (Win32Exception ex)
        {
            LogToolMissing(ytdlp.Path, ex.Message);
            return YtdlpRun.Failed(YouTubeAnswer.CheckFailed, "yt-dlp can't be run here.");
        }

        process.BeginErrorReadLine();
        process.BeginOutputReadLine();
        using CancellationTokenSource timeout = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken,
            enough.Token);
        timeout.CancelAfter(limit);
        try
        {
            await process
                .WaitForExitAsync(timeout.Token)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException)
            when (enough.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            process.Kill(entireProcessTree: true);
            await process
                .WaitForExitAsync(CancellationToken.None)
                .ConfigureAwait(false);
            return new YtdlpRun("", null);
        }
        catch (OperationCanceledException)
        {
            // yt-dlp and the ffmpeg it started: a half download is left in its folder, which the caller removes.
            process.Kill(entireProcessTree: true);
            await process
                .WaitForExitAsync(CancellationToken.None)
                .ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            LogTimedOut(limit);
            return YtdlpRun.Failed(YouTubeAnswer.CheckFailed, "yt-dlp took too long.");
        }

        if (process.ExitCode == 0)
        {
            return new YtdlpRun("", null);
        }

        string[] lines;
        lock (errors)
        {
            lines = [.. errors];
        }

        (YouTubeAnswer answer, string reason) = YtdlpErrors.Classify(lines, process.ExitCode);
        LogRefused(answer, reason);
        return YtdlpRun.Failed(answer, reason) with { Silent = YtdlpErrors.ReasonOf(lines).Length == 0 };
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

    [LoggerMessage(Level = LogLevel.Warning, Message = "yt-dlp ended without leaving {Id}'s file and metadata.")]
    private partial void LogNoDownload(string id);

    [LoggerMessage(Level = LogLevel.Debug, Message = "yt-dlp (download): {Line}")]
    private partial void LogDownloadLine(string line);

    [LoggerMessage(Level = LogLevel.Warning, Message = "yt-dlp answered, but not with {Id}'s metadata.")]
    private partial void LogUnexpectedAnswer(string id);

    /// <summary>A run's output, or why it failed; <see cref="Silent"/>: it failed without an error line.</summary>
    private readonly record struct YtdlpRun(string Output, (YouTubeAnswer Answer, string Reason)? Failure)
    {
        public bool Silent { get; init; }

        public static YtdlpRun Failed(YouTubeAnswer answer, string reason) => new("", (answer, reason));
    }
}
