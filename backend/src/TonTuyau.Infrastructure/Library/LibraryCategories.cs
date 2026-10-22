using Microsoft.EntityFrameworkCore;
using TonTuyau.Infrastructure.Accounts;

namespace TonTuyau.Infrastructure.Library;

/// <summary>
/// The rows of <c>categories</c> the videos written point to: those the library has, new ones added in the same save.
/// YouTube gives a video one category; TubeArchivist and yt-dlp, a list of it (or none): the first one counts.
/// </summary>
internal static class LibraryCategories
{
    /// <returns>The categories of these videos, by name.</returns>
    public static async Task<Dictionary<string, CategoryRow>> FindOrAddAsync(
        TonTuyauDbContext db,
        IEnumerable<IReadOnlyList<string>> categoriesOfEach,
        CancellationToken cancellationToken)
    {
        string[] names = [.. categoriesOfEach
            .Select(NameOf)
            .OfType<string>()
            .Distinct(StringComparer.Ordinal)];
        Dictionary<string, CategoryRow> categories = await db.Categories
            .Where(c => names.Contains(c.Name))
            .ToDictionaryAsync(c => c.Name, StringComparer.Ordinal, cancellationToken)
            .ConfigureAwait(false);
        foreach (string name in names.Where(n => !categories.ContainsKey(n)))
        {
            CategoryRow category = new() { Name = name };
            db.Categories.Add(category);
            categories[name] = category;
        }

        return categories;
    }

    /// <returns>The video's category among those found; null when it has none.</returns>
    public static CategoryRow? Of(IReadOnlyList<string> categories, IReadOnlyDictionary<string, CategoryRow> found) =>
        NameOf(categories) is { } name ? found[name] : null;

    private static string? NameOf(IReadOnlyList<string> categories) =>
        categories.FirstOrDefault(c => !string.IsNullOrWhiteSpace(c));
}
