namespace TonTuyau.Application.YouTube;

/// <summary>
/// TonTuyau's own facts about videos and channels (1.5), read for the sync's writes. They live in PostgreSQL, the source
/// of truth: Elasticsearch is an index, rebuilt from TubeArchivist and these facts.
/// </summary>
public interface IYouTubeFactsSource
{
    /// <returns>The facts of those of the videos TonTuyau has any about, by id.</returns>
    Task<IReadOnlyDictionary<string, VideoFacts>> GetVideosAsync(
        IReadOnlyCollection<string> ids,
        CancellationToken cancellationToken);

    Task<IReadOnlyDictionary<string, ChannelFacts>> GetChannelsAsync(
        IReadOnlyCollection<string> ids,
        CancellationToken cancellationToken);
}
