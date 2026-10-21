namespace TonTuyau.Application.Dashboard;

/// <summary>A page of the dashboard's channels (alpha.3), as the controller binds it: raw values, null = not given.</summary>
/// <param name="Q">Words of the channel's name, accents and case ignored.</param>
/// <param name="Gone">True: only the channels with videos gone from YouTube.</param>
public sealed record DashboardChannelsRequest(
    string? Q,
    string? Sort,
    string? Order,
    bool? Gone,
    string? Cursor,
    int? Size);
