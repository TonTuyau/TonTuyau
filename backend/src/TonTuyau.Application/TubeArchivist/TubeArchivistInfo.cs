namespace TonTuyau.Application.TubeArchivist;

public sealed record TubeArchivistInfo(string Version);

/// <summary>True totals from <c>/api/stats/video/</c>.</summary>
public sealed record LibraryStats(long Total, long Videos, long Shorts, long Streams, long Active, long Inactive);
