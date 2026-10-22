using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace TonTuyau.YouTube;

/// <summary>
/// Where yt-dlp unpacks itself (alpha.6). The image's yt-dlp (<c>yt-dlp_linux</c>) is a PyInstaller program: at each
/// start it unpacks into a new <c>_MEI…</c> folder of the temp folder (about 85 MB), removed when it ends by itself.
/// Stopped instead (a listing read far enough, a timeout, a cancel), the folder stayed: 1,205 of them, 104 GB, filled
/// the disk in a few days. Each run started directly gets a folder of its own (its <c>TMPDIR</c>), deleted once it's
/// over, however it ended; what runs stopped before the backend started left (those of Ytdlp.NET's runs, which keep the
/// backend's temp folder, too) is swept once.
/// </summary>
internal sealed partial class YtdlpTempFolders(ILogger<YtdlpTempFolders> logger)
{
    /// <summary>The prefix of the folders PyInstaller unpacks into.</summary>
    public const string Unpacked = "_MEI";

    private readonly Lock _gate = new();
    private bool _swept;

    /// <summary>The temp folder the runs' own folders are made in.</summary>
    public string Temp { get; init; } = Path.GetTempPath();

    /// <summary>When this backend started: older leftovers are no run's of its (theirs are newer).</summary>
    public DateTime Started { get; init; } = StartOfThisProcess();

    private string Root => Path.Combine(Temp, "tontuyau-ytdlp");

    /// <summary>A new folder for one run (its <c>TMPDIR</c>); the first one sweeps the leftovers, in the background.</summary>
    public string NewFolder()
    {
        lock (_gate)
        {
            if (!_swept)
            {
                _swept = true;
                _ = Task.Run(Sweep);
            }
        }

        string folder = Path.Combine(Root, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        return folder;
    }

    /// <summary>Deletes a run's folder, with what yt-dlp left in it. Not there any more: nothing to do.</summary>
    public void Remove(string folder)
    {
        try
        {
            if (Directory.Exists(folder))
            {
                Directory.Delete(folder, recursive: true);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Swept at the next start.
            LogNotRemoved(ex, folder);
        }
    }

    /// <summary>
    /// What runs stopped before this backend started left: the runs' folders, and the <c>_MEI…</c> folders of the temp
    /// folder. Only those older than this backend: a run going on keeps its own.
    /// </summary>
    /// <returns>How many folders were removed.</returns>
    public int Sweep()
    {
        int removed = 0;
        IEnumerable<string> leftovers = Directory.Exists(Root) ? Directory.EnumerateDirectories(Root) : [];
        if (Directory.Exists(Temp))
        {
            leftovers = leftovers.Concat(Directory.EnumerateDirectories(Temp, $"{Unpacked}*"));
        }

        foreach (string folder in leftovers.ToArray())
        {
            if (Directory.GetLastWriteTimeUtc(folder) < Started)
            {
                Remove(folder);
                removed += Directory.Exists(folder) ? 0 : 1;
            }
        }

        if (removed > 0)
        {
            LogSwept(removed);
        }

        return removed;
    }

    private static DateTime StartOfThisProcess()
    {
        using Process self = Process.GetCurrentProcess();
        return self.StartTime.ToUniversalTime();
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "yt-dlp's temp folder {Folder} couldn't be removed.")]
    private partial void LogNotRemoved(Exception exception, string folder);

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "{Count} temp folders yt-dlp left before this start removed.")]
    private partial void LogSwept(int count);
}
