using TonTuyau.Application.Store;

namespace TonTuyau.Application.Dashboard;

/// <param name="Gone">Videos of the channel no longer available on YouTube.</param>
public sealed record ChannelStats(TypeCounts Counts, long Gone);
