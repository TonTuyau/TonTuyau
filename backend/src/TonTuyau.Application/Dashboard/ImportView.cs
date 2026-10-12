using TonTuyau.Application.Store;

namespace TonTuyau.Application.Dashboard;

/// <summary>One sync run that brought new videos.</summary>
/// <param name="At">When the run started.</param>
/// <param name="ByType">Null for the runs of TonTuyau 1.1 and older: they didn't record the types.</param>
public sealed record ImportView(DateTimeOffset At, int Total, TypeCounts? ByType);
