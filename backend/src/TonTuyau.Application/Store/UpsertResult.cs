namespace TonTuyau.Application.Store;

/// <param name="Created">Didn't exist before.</param>
/// <param name="Updated">Existed with different content.</param>
/// <param name="Unchanged">Existed with the same content: not rewritten.</param>
public sealed record UpsertResult(int Created, int Updated, int Unchanged)
{
    public static readonly UpsertResult None = new(0, 0, 0);

    /// <summary>Ids that were created or updated (their art may need copying again).</summary>
    public IReadOnlyList<string> ChangedIds { get; init; } = [];

    public int Total => Created + Updated + Unchanged;

    public UpsertResult Add(UpsertResult other)
    {
        ArgumentNullException.ThrowIfNull(other);
        return new(Created + other.Created, Updated + other.Updated, Unchanged + other.Unchanged)
        {
            ChangedIds = [.. ChangedIds, .. other.ChangedIds],
        };
    }

    public bool Equals(UpsertResult? other) =>
        other is not null
        && (Created, Updated, Unchanged) == (other.Created, other.Updated, other.Unchanged)
        && ChangedIds.SequenceEqual(other.ChangedIds);

    public override int GetHashCode() => HashCode.Combine(Created, Updated, Unchanged);
}
