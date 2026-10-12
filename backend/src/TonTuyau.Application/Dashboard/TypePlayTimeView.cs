using TonTuyau.Application.Library;

namespace TonTuyau.Application.Dashboard;

/// <param name="Share">This type's part of the whole play time, from 0 to 1 (0 when there's no play time at all).</param>
/// <param name="Average">The average length of one video of this type (zero without any).</param>
public sealed record TypePlayTimeView(VideoType Type, long Count, TimeSpan Total, double Share, TimeSpan Average);
