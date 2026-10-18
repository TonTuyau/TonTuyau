using TonTuyau.Application.Paging;
using TonTuyau.Application.YouTube;

namespace TonTuyau.Application.Store;

/// <summary>
/// The library's videos with what the index (Elasticsearch) answers: lists, search, counts, suggestions. Writes go to
/// the write alias, reads to the read alias.
/// </summary>
public interface IVideoRepository : IVideoStore
{
    /// <summary>
    /// One page of videos matching the query, sorted, with the exact total. Pages follow each other with
    /// <see cref="PageRequest.After"/> (<c>search_after</c>), so there's no depth limit.
    /// </summary>
    /// <exception cref="Validation.ValidationException">The position doesn't fit the query's sort.</exception>
    Task<StorePage<VideoListItem>> FindAsync(VideoQuery query, PageRequest page, CancellationToken cancellationToken);

    /// <returns>How many videos of the channel are in each state on YouTube (1.5); a state without any isn't there.</returns>
    Task<IReadOnlyDictionary<YouTubeState, long>> CountYouTubeStatesAsync(
        string channelId,
        CancellationToken cancellationToken);

    /// <returns>Per channel id: how many videos, shorts and streams it has. Channels without any are left out.</returns>
    Task<IReadOnlyDictionary<string, TypeCounts>> CountByTypeAsync(
        IReadOnlyCollection<string> channelIds,
        CancellationToken cancellationToken);

    /// <returns>Videos whose title starts words with what was typed (accents and case ignored), most viewed first.</returns>
    Task<IReadOnlyList<TextMatch>> SuggestTitlesAsync(string prefix, int size, CancellationToken cancellationToken);
}
