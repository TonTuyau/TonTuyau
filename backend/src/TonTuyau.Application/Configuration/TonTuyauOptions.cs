namespace TonTuyau.Application.Configuration;

/// <param name="TubeArchivist">Where to import from (2.0); null without its settings: TonTuyau runs without it.</param>
public sealed record TonTuyauOptions(
    TubeArchivistOptions? TubeArchivist,
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
public sealed record YouTubeOptions(
    string YtdlpPath = TonTuyauOptionsReader.DefaultYtdlpPath,
    string DenoPath = TonTuyauOptionsReader.DefaultDenoPath,
    string ToolsRoot = TonTuyauOptionsReader.DefaultToolsRoot);

/// <param name="IndexPrefix">Prefix of every TonTuyau index and alias, e.g. <c>tontuyau</c> → <c>tontuyau-videos</c>.</param>
public sealed record ElasticsearchOptions(
    string Url,
    string Username,
    string Password,
    string IndexPrefix = TonTuyauOptionsReader.DefaultIndexPrefix);

/// <param name="MediaRoot">Where TubeArchivist's <c>/youtube</c> share is mounted (read-only).</param>
/// <param name="ArtRoot">TonTuyau's own volume for copied thumbnails and channel art.</param>
public sealed record StorageOptions(string MediaRoot, string ArtRoot);

/// <param name="TaResultWindow">TubeArchivist's list window (10,000: its Elasticsearch <c>max_result_window</c>).</param>
/// <param name="ArtResizeParallelism">
/// How many images the import resizes at once (default: the machine's core count).
/// </param>
/// <param name="TimeZone">The time zone the dashboard counts its days and weeks in.</param>
public sealed record SyncOptions(
    string TimeZone,
    int TaResultWindow = TonTuyauOptionsReader.DefaultTaResultWindow,
    int ArtResizeParallelism = 1);
