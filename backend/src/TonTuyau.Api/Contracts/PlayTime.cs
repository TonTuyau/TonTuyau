using TonTuyau.Application.Dashboard;

namespace TonTuyau.Api.Contracts;

/// <param name="TotalSeconds">Every video's duration added up.</param>
/// <param name="Types">Videos, shorts and streams, in that order (all three, even without any).</param>
public sealed record PlayTime(long TotalSeconds, IReadOnlyList<TypePlayTime> Types)
{
    public static explicit operator PlayTime(PlayTimeView view)
    {
        ArgumentNullException.ThrowIfNull(view);
        return new PlayTime(
            (long)Math.Round(view.Total.TotalSeconds),
            [.. view.Types.Select(t => (TypePlayTime)t)]);
    }
}
