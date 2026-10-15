using TonTuyau.Application.Store;

namespace TonTuyau.Application.Dashboard;

/// <param name="Gone">Videos of the channel TonTuyau found gone from YouTube.</param>
/// <param name="RetryLater">Videos whose last check had no answer to trust (1.5).</param>
/// <param name="NotChecked">Videos TonTuyau hasn't asked YouTube about yet (1.5).</param>
public sealed record ChannelStats(TypeCounts Counts, long Gone, long RetryLater, long NotChecked);
