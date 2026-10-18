namespace TonTuyau.Infrastructure.Library;

/// <summary>What the art store holds for one picture (JSON): the original's width, the resized versions' widths.</summary>
public sealed class ArtVersionsRow
{
    public int OriginalWidth { get; set; }

    public List<int> Widths { get; set; } = [];
}
