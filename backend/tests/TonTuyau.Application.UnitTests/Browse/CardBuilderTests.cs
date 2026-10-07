using TonTuyau.Application.Browse;
using TonTuyau.Application.Library;
using TonTuyau.Application.Store;
using TonTuyau.Application.Sync;

namespace TonTuyau.Application.UnitTests.Browse;

/// <summary>Images on cards come from what the sync recorded (QA09): the disk is only read when nothing is recorded.</summary>
public sealed class CardBuilderTests
{
    private const string Thumbnail = "/cache/videos/a/abcdefghijk.jpg";

    private readonly CountingArt _art = new();

    [Fact]
    public void A_recorded_image_is_used_as_is_without_touching_the_disk()
    {
        ArtVersions recorded = new(1280, [360, 720]);

        ArtVersions? image = Builder().Image(Thumbnail, recorded, ArtSizes.VideoThumbnail);

        Assert.Same(recorded, image);
        Assert.Equal(0, _art.Described);
    }

    [Fact]
    public void An_image_not_recorded_yet_is_looked_up_in_the_art_store()
    {
        _art.Answer = new ArtVersions(640, [360]);

        ArtVersions? image = Builder().Image(Thumbnail, null, ArtSizes.VideoThumbnail);

        Assert.Equal(new ArtVersions(640, [360]), image);
        (string path, IReadOnlyList<int> widths) = Assert.Single(_art.Asked);
        Assert.Equal(Thumbnail, path);
        Assert.Equal([360, 720], widths);
    }

    [Fact]
    public void No_image_when_the_store_has_none()
    {
        Assert.Null(Builder().Image(Thumbnail, null, ArtSizes.VideoThumbnail));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("/etc/passwd")]
    [InlineData("/cache/videos/../x/abcdefghijk.jpg")]
    public void A_path_that_isnt_an_art_path_has_no_image_even_if_recorded(string? path)
    {
        Assert.Null(Builder().Image(path, new ArtVersions(1280, [360]), ArtSizes.VideoThumbnail));
        Assert.Equal(0, _art.Described);
    }

    private CardBuilder Builder() => new(null!, null!, _art);

    private sealed class CountingArt : IArtStore
    {
        public ArtVersions? Answer { get; set; }

        public List<(string Path, IReadOnlyList<int> Widths)> Asked { get; } = [];

        public int Described => Asked.Count;

        public ArtVersions? Describe(string cachePath, IReadOnlyList<int> widths)
        {
            Asked.Add((cachePath, widths));
            return Answer;
        }

        public bool Exists(string cachePath) => throw new NotSupportedException();

        public Task SaveAsync(string cachePath, Stream content, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public void Delete(string cachePath) => throw new NotSupportedException();

        public StoredFile? Find(string cachePath) => throw new NotSupportedException();

        public StoredFile? FindVersion(string cachePath, int width) => throw new NotSupportedException();
    }
}
