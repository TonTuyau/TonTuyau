using System.Globalization;
using TonTuyau.Application.Paging;
using TonTuyau.Application.Store;

namespace TonTuyau.Application.Browse;

/// <summary>
/// What a cursor is tied to: the list (<paramref name="scope"/>) and everything that decides its order and content.
/// The page size isn't part of it: a client may change it between pages.
/// </summary>
internal static class Fingerprints
{
    /// <param name="period">A search's <c>published</c> period. Its cut-off instant isn't part of the fingerprint: it
    /// is computed on the first page and travels in the cursor.</param>
    public static string Of(string scope, VideoQuery query, PublishedPeriod? period = null) => Join(
        scope,
        query.Text is null ? null : string.Join(' ', query.Text.Words),
        query.Text?.Prefix,
        query.Filter.Type?.ToString(),
        query.Filter.ChannelId,
        query.Filter.PublishedFrom?.UtcTicks.ToString(CultureInfo.InvariantCulture),
        period?.ToString(),
        query.Filter.Duration?.ToString(),
        query.Filter.ExcludeId,
        query.Sort.ToString());

    public static string Of(string scope, ChannelQuery query) =>
        Join(
            scope,
            query.NamePrefix,
            query.Text is null ? null : string.Join(' ', query.Text.Words),
            query.Text?.Prefix,
            query.Sort.ToString());

    // Length-prefixed, so ("a|b", "c") and ("a", "b|c") never give the same text; "-" = not given.
    private static string Join(params string?[] parts) =>
        CursorCodec.Fingerprint(string.Concat(parts.Select(p => p is null ? "-|" : $"{p.Length}:{p}|")));
}
