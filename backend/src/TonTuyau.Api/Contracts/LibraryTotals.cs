using TonTuyau.Application.Dashboard;

namespace TonTuyau.Api.Contracts;

/// <param name="Gone">Videos TonTuyau keeps that are no longer available on YouTube.</param>
public sealed record LibraryTotals(long Channels, long Videos, long Shorts, long Streams, long Gone)
{
    public static explicit operator LibraryTotals(LibraryTotalsView view)
    {
        ArgumentNullException.ThrowIfNull(view);
        return new LibraryTotals(view.Channels, view.Videos, view.Shorts, view.Streams, view.Gone);
    }
}
