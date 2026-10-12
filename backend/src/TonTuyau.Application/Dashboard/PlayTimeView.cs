namespace TonTuyau.Application.Dashboard;

/// <param name="Total">Every video's duration added up.</param>
/// <param name="Types">Videos, shorts and streams, in that order (all three, even without any).</param>
public sealed record PlayTimeView(TimeSpan Total, IReadOnlyList<TypePlayTimeView> Types);
