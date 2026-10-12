using TonTuyau.Application.Library;

namespace TonTuyau.Application.Dashboard;

/// <param name="Duration">The sum of the videos' durations.</param>
public sealed record TypeStats(VideoType Type, long Count, TimeSpan Duration);
