namespace TonTuyau.Application.Paging;

/// <summary>One value of a position in a sorted list (what Elasticsearch calls a <c>search_after</c> value).</summary>
public abstract record SortValue;

public sealed record TextSortValue(string Value) : SortValue;

public sealed record IntegerSortValue(long Value) : SortValue;

public sealed record RealSortValue(double Value) : SortValue;

/// <summary>Where a page ends: the sort values of its last item. The next page starts right after it.</summary>
public sealed record SortPosition(IReadOnlyList<SortValue> Values);

/// <summary>What a cursor brings back: where the next page starts, and the cut-off frozen on the first page.</summary>
/// <param name="Since">A search's <c>published</c> cut-off, computed on the first page (null: none).</param>
public sealed record PageCursor(SortPosition After, DateTimeOffset? Since);

/// <param name="After">Null for the first page.</param>
public sealed record PageRequest(int Size, SortPosition? After);

/// <summary>A page as the store returns it.</summary>
/// <param name="Next">Position of the last item, or null when nothing comes after this page.</param>
/// <param name="Total">Exact number of items matching the query, over all pages.</param>
public sealed record StorePage<T>(IReadOnlyList<T> Items, SortPosition? Next, long Total);

/// <summary>A page as the API returns it: the position is turned into an opaque cursor.</summary>
public sealed record ResultPage<T>(IReadOnlyList<T> Items, string? NextCursor, long Total);
