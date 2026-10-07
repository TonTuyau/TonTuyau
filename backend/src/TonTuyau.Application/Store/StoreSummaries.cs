using TonTuyau.Application.Library;

namespace TonTuyau.Application.Store;

/// <summary>What the sync needs to know about a stored video to plan (and log) its deletion.</summary>
public sealed record VideoSummary(
    string Id,
    string Title,
    string ChannelId,
    VideoType Type,
    DateTimeOffset Downloaded,
    string? SeenInRun,
    string ThumbnailPath);

/// <summary>What the sync needs to know about a stored channel to plan (and log) its deletion.</summary>
public sealed record ChannelSummary(string Id, string Name, string? SeenInRun, IReadOnlyList<string> ArtPaths);
