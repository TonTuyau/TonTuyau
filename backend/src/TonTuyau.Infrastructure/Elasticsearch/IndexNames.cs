using System.Globalization;
using TonTuyau.Application.Store;

namespace TonTuyau.Infrastructure.Elasticsearch;

/// <summary>
/// Names of TonTuyau's indices. Each store index has concrete generations (<c>tontuyau-videos-v1</c>, <c>-v2</c>…)
/// behind a read alias (<c>tontuyau-videos</c>) and a write alias (<c>tontuyau-videos-write</c>).
/// </summary>
internal sealed class IndexNames(string prefix)
{
    public string Prefix { get; } = prefix;

    public static string Name(StoreIndex index) => index switch
    {
        StoreIndex.Videos => "videos",
        StoreIndex.Channels => "channels",
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };

    /// <summary>Sync state read by id (lock, checkpoint, pending art). A plain index: rebuilds never touch it.</summary>
    public string Sync => $"{Prefix}-sync";

    /// <summary>Sync run history (searched by mode, outcome, start). A plain index too.</summary>
    public string SyncRuns => $"{Prefix}-sync-runs";

    public string ReadAlias(StoreIndex index) => $"{Prefix}-{Name(index)}";

    public string WriteAlias(StoreIndex index) => $"{Prefix}-{Name(index)}-write";

    public string Generation(StoreIndex index, int version) =>
        $"{Prefix}-{Name(index)}-v{version.ToString(CultureInfo.InvariantCulture)}";

    /// <returns>The generation number of a concrete index of this store index, or null if it isn't one.</returns>
    public int? VersionOf(StoreIndex index, string concreteIndex)
    {
        string start = $"{Prefix}-{Name(index)}-v";
        return concreteIndex.StartsWith(start, StringComparison.Ordinal)
            && int.TryParse(
                concreteIndex.AsSpan(start.Length),
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out int version)
            && version > 0
            ? version
            : null;
    }
}
