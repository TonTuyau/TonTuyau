namespace TonTuyau.Application.Browse;

// API requests as the controllers bind them from the query string: raw values, checked by the validators below
// (RequestValidators.cs) before a service turns them into store queries. Null = parameter not given.

public sealed record HomeRequest(int? Size);

/// <param name="Exclude">A video left out of the list and its total (the short being watched, contract v1.3).</param>
public sealed record VideoListRequest(string? Type, string? Sort, string? Exclude, string? Cursor, int? Size);

/// <param name="Exclude">A video left out of the list and its total (the one being watched, "More from…").</param>
public sealed record ChannelVideosRequest(
    string ChannelId,
    string? Type,
    string? Sort,
    string? Exclude,
    string? Cursor,
    int? Size);

/// <param name="Q">As-you-type filter on the channel name.</param>
public sealed record ChannelListRequest(string? Q, string? Sort, string? Cursor, int? Size);

public sealed record VideoSearchRequest(
    string? Q,
    string? Kind,
    string? Type,
    string? Channel,
    string? Published,
    string? Duration,
    string? Sort,
    string? Cursor,
    int? Size);

public sealed record SuggestRequest(string? Q, int? Size);
