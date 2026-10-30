using System.Globalization;

namespace TonTuyau.Application.Sync;

/// <summary>
/// A progress line, e.g. <c>[06:10] videos        37 / 120 channels · 12,000 videos · art 2,300 copied, 6,100
/// resized</c>. A total ("x / y") is shown only when it's exactly known: TubeArchivist's page counts and hit totals
/// aren't (last_page is wrong, total_hits stops at 10,000).
/// </summary>
public static class SyncProgressText
{
    /// <summary>The phase column's width (the longest label).</summary>
    public const int LabelWidth = 13;

    public static string Line(SyncProgress progress, TimeSpan elapsed)
    {
        ArgumentNullException.ThrowIfNull(progress);
        return $"[{Elapsed(elapsed)}] {Label(progress.Phase).PadRight(LabelWidth)} {Detail(progress)}";
    }

    /// <summary><c>mm:ss</c>, or <c>h:mm:ss</c> from an hour on.</summary>
    public static string Elapsed(TimeSpan elapsed)
    {
        TimeSpan time = elapsed < TimeSpan.Zero ? TimeSpan.Zero : elapsed;
        int hours = (int)time.TotalHours;
        return hours > 0
            ? string.Create(CultureInfo.InvariantCulture, $"{hours}:{time.Minutes:00}:{time.Seconds:00}")
            : string.Create(CultureInfo.InvariantCulture, $"{time.Minutes:00}:{time.Seconds:00}");
    }

    /// <summary>
    /// "x / y" when the total is known and possible, else "x": an unknown total is never guessed, and a total smaller
    /// than what's already done is wrong, so it isn't shown either.
    /// </summary>
    public static string OutOf(int done, int? total) =>
        total is not null && done <= total.Value ? $"{Number(done)} / {Number(total.Value)}" : Number(done);

    public static string Label(SyncPhase phase) => phase switch
    {
        SyncPhase.Channels => "channels",
        SyncPhase.ChannelVideos => "videos",
        SyncPhase.MediaCheck => "media check",
        SyncPhase.ArtRetry => "art retry",
        SyncPhase.Indexing => "indexing",
        SyncPhase.IndexSwitch => "index switch",
        SyncPhase.YouTubeChannel => "yt channel",
        SyncPhase.YouTubeVideos => "yt videos",
        SyncPhase.YouTubeDownload => "download",
        SyncPhase.YouTubeLibrary => "yt library",
        SyncPhase.Purge => "purge",
        _ => phase.ToString(),
    };

    private static string Detail(SyncProgress p) => p.Phase switch
    {
        SyncPhase.Channels => $"{OutOf(p.Done, p.Total)} read" + Art(p.Counts),
        SyncPhase.ChannelVideos =>
            $"{OutOf(p.Done, p.Total)} channels · {Number(p.Counts.VideosRead)} videos" + Art(p.Counts),
        SyncPhase.MediaCheck => $"{Number(p.FilesChecked)} files checked · {Number(p.FilesMissing)} missing",
        SyncPhase.ArtRetry => $"{OutOf(p.Done, p.Total)} images retried",
        SyncPhase.Indexing => $"{OutOf(p.Done, p.Total)} written to the new indices",
        SyncPhase.IndexSwitch => "switching to the new indices",
        SyncPhase.YouTubeChannel => "reading the channel",
        SyncPhase.YouTubeVideos => $"{OutOf(p.Done, p.Total)} read" + YouTube(p.Counts),
        SyncPhase.YouTubeDownload => p.Done == 0 ? "downloading" : "recording it in the library",
        SyncPhase.YouTubeLibrary => $"{OutOf(p.Done, p.Total)} channels and videos read" + YouTube(p.Counts),
        SyncPhase.Purge => $"{OutOf(p.Done, p.Total)} videos purged",
        _ => Number(p.Done),
    };

    /// <summary>What YouTube answered so far (a refresh, 1.5).</summary>
    private static string YouTube(SyncCounts counts) =>
        $" · {Number(counts.FoundOnYouTube)} found ({Number(counts.UnlistedOnYouTube)} unlisted), "
        + $"{Number(counts.GoneFromYouTube)} gone, "
        + $"{Number(counts.PrivateOnYouTube)} private, {Number(counts.CheckFailed)} to retry";

    /// <summary>The art made so far, once there's some (later full runs mostly have none).</summary>
    private static string Art(SyncCounts counts) =>
        counts.ArtCopied == 0 && counts.ArtResized == 0
            ? ""
            : $" · art {Number(counts.ArtCopied)} copied, {Number(counts.ArtResized)} resized";

    private static string Number(int value) => value.ToString("N0", CultureInfo.InvariantCulture);
}
