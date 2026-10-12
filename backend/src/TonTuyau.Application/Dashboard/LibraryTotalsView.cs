namespace TonTuyau.Application.Dashboard;

/// <param name="Gone">Videos TonTuyau keeps that are no longer available on YouTube.</param>
public sealed record LibraryTotalsView(long Channels, long Videos, long Shorts, long Streams, long Gone);
