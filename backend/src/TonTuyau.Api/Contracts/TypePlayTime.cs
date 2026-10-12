using TonTuyau.Application.Dashboard;
using TonTuyau.Application.Library;

namespace TonTuyau.Api.Contracts;

/// <param name="Share">This type's part of the whole play time, from 0 to 1.</param>
/// <param name="AverageSeconds">The average length of one video of this type (0 without any).</param>
public sealed record TypePlayTime(VideoType Type, long Count, long Seconds, double Share, long AverageSeconds)
{
    public static explicit operator TypePlayTime(TypePlayTimeView view)
    {
        ArgumentNullException.ThrowIfNull(view);
        return new TypePlayTime(
            view.Type,
            view.Count,
            (long)Math.Round(view.Total.TotalSeconds),
            view.Share,
            (long)Math.Round(view.Average.TotalSeconds));
    }
}
