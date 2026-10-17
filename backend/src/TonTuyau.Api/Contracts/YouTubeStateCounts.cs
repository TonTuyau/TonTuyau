namespace TonTuyau.Api.Contracts;

/// <summary>How many videos are in each state on YouTube (contract v1.12).</summary>
/// <param name="Unlisted">On YouTube, not listed (1.5, contract v1.16).</param>
/// <param name="Private">Made private by their owner (1.5, contract v1.16).</param>
public sealed record YouTubeStateCounts(
    long NotChecked,
    long OnYouTube,
    long Unlisted,
    long Private,
    long RetryLater,
    long Gone);
