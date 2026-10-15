using TonTuyau.Application.YouTube;

namespace TonTuyau.Api.Contracts;

/// <summary>The versions offered, the latest first: every official release with a Linux program, and the nightly
/// builds of the last 2 months.</summary>
public sealed record YtdlpVersionsResponse(
    IReadOnlyList<YtdlpVersionResponse> Official,
    IReadOnlyList<YtdlpVersionResponse> Nightly)
{
    public static explicit operator YtdlpVersionsResponse(YtdlpVersions versions)
    {
        ArgumentNullException.ThrowIfNull(versions);
        return new YtdlpVersionsResponse(Of(versions.Official), Of(versions.Nightly));
    }

    private static YtdlpVersionResponse[] Of(IReadOnlyList<YtdlpRelease> releases) =>
        [.. releases.Select(r => new YtdlpVersionResponse(r.Version, r.PublishedAt.UtcDateTime))];
}
