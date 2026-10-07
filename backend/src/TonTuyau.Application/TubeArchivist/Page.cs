namespace TonTuyau.Application.TubeArchivist;

/// <summary>
/// One page of a TubeArchivist list. TubeArchivist's own <c>last_page</c> is unreliable, so it isn't exposed:
/// the end of a list is a page with nothing on it (<see cref="IsPastEnd"/>).
/// </summary>
/// <param name="Number">1-based page number.</param>
/// <param name="PageSize">Page size TubeArchivist used for this response (a TA setting; never assumed).</param>
/// <param name="TotalHits">Total reported by TA. Capped at 10,000 when <paramref name="TotalIsCapped"/>.</param>
public sealed record Page<T>(int Number, int PageSize, IReadOnlyList<T> Items, long TotalHits, bool TotalIsCapped)
{
    /// <summary>Documents on this page that failed validation. They are not in <see cref="Items"/>.</summary>
    public IReadOnlyList<RejectedItem> Rejected { get; init; } = [];

    /// <summary>Nothing at all on this page (no valid nor rejected item): past the end of the list.</summary>
    public bool IsPastEnd => Items.Count == 0 && Rejected.Count == 0;
}

/// <summary>A TubeArchivist document that failed validation, and why.</summary>
/// <param name="Id">The document's id when it has a valid one (so it can still be counted as "seen"), else null.</param>
public sealed record RejectedItem(string? Id, string Subject, IReadOnlyList<Validation.ValidationError> Errors);
