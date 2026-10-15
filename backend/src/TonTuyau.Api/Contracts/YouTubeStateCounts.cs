namespace TonTuyau.Api.Contracts;

/// <summary>How many videos are in each state on YouTube (contract v1.12).</summary>
public sealed record YouTubeStateCounts(long NotChecked, long OnYouTube, long RetryLater, long Gone);
