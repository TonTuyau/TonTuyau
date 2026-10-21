using TonTuyau.Application.Pump;

namespace TonTuyau.Api.Contracts;

/// <summary>A YouTube video or channel as the browser extension's button shows it (contract v2.6).</summary>
/// <param name="State">
/// absent (it can be added) | fileMissing (a video of the library without its file: adding it downloads the file) |
/// waiting (its run waits, goes on or is on hold) | inTonTuyau (a video with its file, a channel subscribed to).
/// </param>
/// <param name="RunId">While waiting: the run adding it (the Tasks page shows it); null otherwise.</param>
public sealed record PumpItemResponse(PumpItemState State, string? RunId)
{
    public static explicit operator PumpItemResponse(PumpItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        return new PumpItemResponse(item.State, item.RunId);
    }
}
