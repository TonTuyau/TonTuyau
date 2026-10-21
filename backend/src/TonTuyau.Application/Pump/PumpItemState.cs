namespace TonTuyau.Application.Pump;

/// <summary>Where a YouTube video or channel stands in TonTuyau (2.0, B7), as the browser extension's button shows it.</summary>
public enum PumpItemState
{
    /// <summary>Not in TonTuyau (a channel: not subscribed to): it can be added.</summary>
    Absent,

    /// <summary>A video of the library whose file is missing: adding it downloads its file.</summary>
    FileMissing,

    /// <summary>Its download or "Add a channel" waits in the queue, goes on, or is on hold.</summary>
    Waiting,

    /// <summary>In TonTuyau: a video of the library with its file, a channel subscribed to.</summary>
    InTonTuyau,
}
