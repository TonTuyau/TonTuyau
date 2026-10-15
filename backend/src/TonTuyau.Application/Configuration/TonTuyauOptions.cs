namespace TonTuyau.Application.Configuration;

public sealed record TonTuyauOptions(
    TubeArchivistOptions TubeArchivist,
    ElasticsearchOptions Elasticsearch,
    StorageOptions Storage,
    SyncOptions Sync,
    DatabaseOptions Database,
    AccountOptions Accounts,
    YouTubeOptions YouTube)
{
    /// <summary>Values the reader couldn't parse (e.g. a number that isn't one); reported by the validator.</summary>
    public IReadOnlyList<Validation.ValidationError> Unparsable { get; init; } = [];
}

public sealed record TubeArchivistOptions(string BaseUrl, string Token);

/// <param name="YtdlpPath">The image's yt-dlp (1.5): a path, or a name found on the PATH; the default and fallback.</param>
/// <param name="DenoPath">The Deno program yt-dlp runs YouTube's JavaScript with; only its version is read here.</param>
/// <param name="ToolsRoot">TonTuyau's own folder for the yt-dlp versions it downloads (a volume in the stack).</param>
/// <param name="ReleasesApiUrl">GitHub's API, where yt-dlp's releases are listed (another only in tests).</param>
/// <param name="DownloadsUrl">GitHub, where yt-dlp's release files are downloaded from (another only in tests).</param>
public sealed record YouTubeOptions(
    string YtdlpPath = TonTuyauOptionsReader.DefaultYtdlpPath,
    string DenoPath = TonTuyauOptionsReader.DefaultDenoPath,
    string ToolsRoot = TonTuyauOptionsReader.DefaultToolsRoot,
    string ReleasesApiUrl = TonTuyauOptionsReader.DefaultReleasesApiUrl,
    string DownloadsUrl = TonTuyauOptionsReader.DefaultDownloadsUrl);

/// <param name="IndexPrefix">Prefix of every TonTuyau index and alias, e.g. <c>tontuyau</c> → <c>tontuyau-videos</c>.</param>
public sealed record ElasticsearchOptions(
    string Url,
    string Username,
    string Password,
    string IndexPrefix = TonTuyauOptionsReader.DefaultIndexPrefix);

/// <param name="MediaRoot">Where TubeArchivist's <c>/youtube</c> share is mounted (read-only).</param>
/// <param name="ArtRoot">TonTuyau's own volume for copied thumbnails and channel art.</param>
public sealed record StorageOptions(string MediaRoot, string ArtRoot);

/// <param name="LogRoot">Where full runs write their deletion lists (<c>&lt;run&gt;-deletions.jsonl</c>).</param>
/// <param name="MaxDeletions">Above this many deletions, a full run deletes nothing and waits for <c>apply-deletions</c>.</param>
/// <param name="IncrementalMarginHours">How far below the watermark an incremental run keeps reading (safety margin).</param>
/// <param name="SchedulerEnabled">False in tests and for CLI-only use.</param>
/// <param name="TaResultWindow">TubeArchivist's list window (10,000: its Elasticsearch <c>max_result_window</c>).</param>
/// <param name="RejectGuardPercent">
/// Format guard: a run stops when more than this share of the documents read so far is rejected (100 = never).
/// </param>
/// <param name="RejectGuardMinDocuments">Format guard: no judgement before this many documents are read (0 = from the first page).</param>
/// <param name="ArtResizeParallelism">How many images the sync resizes at once (default: the machine's core count).</param>
public sealed record SyncOptions(
    string IncrementalCron,
    string FullCron,
    string TimeZone,
    string LogRoot = TonTuyauOptionsReader.DefaultSyncLogRoot,
    int MaxDeletions = TonTuyauOptionsReader.DefaultMaxDeletions,
    int IncrementalMarginHours = TonTuyauOptionsReader.DefaultMarginHours,
    bool SchedulerEnabled = true,
    int TaResultWindow = TonTuyauOptionsReader.DefaultTaResultWindow,
    int RejectGuardPercent = TonTuyauOptionsReader.DefaultRejectGuardPercent,
    int RejectGuardMinDocuments = TonTuyauOptionsReader.DefaultRejectGuardMinDocuments,
    int ArtResizeParallelism = 1);
