namespace TonTuyau.Application.YouTube;

/// <summary>
/// What TonTuyau knows of videos and channels on YouTube (1.5), as the refresh reads it before it reads YouTube again:
/// their state, and once refreshed, their metadata, chapters and comment count. Read from the library's rows (2.0).
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
