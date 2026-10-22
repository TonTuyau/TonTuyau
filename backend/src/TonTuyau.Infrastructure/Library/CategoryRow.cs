namespace TonTuyau.Infrastructure.Library;

/// <summary>
/// A row of <c>categories</c> (2.0): one of YouTube's video categories (Education, Science &amp; Technology…), by its
/// name, once for all the videos in it (<c>videos.category_id</c>).
/// </summary>
public sealed class CategoryRow
{
    public int Id { get; set; }

    public required string Name { get; set; }
}
