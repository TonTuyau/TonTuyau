namespace TonTuyau.Application.Library;

/// <summary>
/// What the art store holds for one copied image, as the sync recorded it (Q9, contract v1.4): the original's width,
/// and the widths of the resized WebP versions that exist, smallest first.
/// </summary>
/// <param name="OriginalWidth">The original image's width in pixels.</param>
/// <param name="Widths">Resized versions that exist, ascending; never one at or above the original's width.</param>
public sealed record ArtVersions(int OriginalWidth, IReadOnlyList<int> Widths)
{
    public bool Equals(ArtVersions? other) =>
        other is not null && OriginalWidth == other.OriginalWidth && Widths.SequenceEqual(other.Widths);

    public override int GetHashCode() => HashCode.Combine(OriginalWidth, Widths.Count);
}
