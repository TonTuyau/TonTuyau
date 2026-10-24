using TonTuyau.Application.Library;

namespace TonTuyau.Application.Dashboard;

/// <param name="Duration">The sum of the videos' durations.</param>
/// <param name="Bytes">The sum of their files' sizes (2.2.0).</param>
public sealed record TypeStats(VideoType Type, long Count, TimeSpan Duration, long Bytes = 0);
