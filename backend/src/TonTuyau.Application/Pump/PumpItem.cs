namespace TonTuyau.Application.Pump;

/// <summary>A video or channel as the browser extension sees it (2.0, B7).</summary>
/// <param name="RunId">While <see cref="PumpItemState.Waiting"/>: the run that adds it; null otherwise.</param>
public sealed record PumpItem(PumpItemState State, string? RunId = null);
