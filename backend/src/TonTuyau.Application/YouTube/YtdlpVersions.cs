namespace TonTuyau.Application.YouTube;

/// <summary>The versions the admin can choose (1.5): every official release, the nightly builds of the last 2 months.</summary>
public sealed record YtdlpVersions(IReadOnlyList<YtdlpRelease> Official, IReadOnlyList<YtdlpRelease> Nightly);
