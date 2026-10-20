using System.Text.RegularExpressions;

namespace TonTuyau.Application.Library;

/// <summary>YouTube id formats, as TubeArchivist stores them. Ids from URLs are checked against these first.</summary>
public static partial class LibraryIds
{
    public static bool IsVideoId(string? id) => id is not null && VideoId().IsMatch(id);

    public static bool IsChannelId(string? id) => id is not null && ChannelId().IsMatch(id);

    /// <summary>
    /// A video's id from what an admin pastes (2.0, "Add a video"): the id itself, or a YouTube link to it
    /// (<c>youtube.com/watch?v=</c>, <c>youtu.be/</c>, <c>/shorts/</c>, <c>/live/</c>, <c>/embed/</c>; <c>www.</c>,
    /// <c>m.</c>, <c>music.</c>, with or without <c>https://</c>).
    /// </summary>
    /// <returns>Null when it's neither.</returns>
    public static string? VideoIdOf(string? text)
    {
        string trimmed = text?.Trim() ?? "";
        if (IsVideoId(trimmed))
        {
            return trimmed;
        }

        if (!Uri.TryCreate(trimmed, UriKind.Absolute, out Uri? link) || link.Scheme is not ("https" or "http"))
        {
            if (!Uri.TryCreate("https://" + trimmed, UriKind.Absolute, out link))
            {
                return null;
            }
        }

        string host = link.Host.ToLowerInvariant();
        host = host.StartsWith("www.", StringComparison.Ordinal) ? host[4..]
            : host.StartsWith("m.", StringComparison.Ordinal) ? host[2..]
            : host.StartsWith("music.", StringComparison.Ordinal) ? host[6..]
            : host;
        string[] path = link.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        string? id = (host, path) switch
        {
            ("youtu.be", [string first, ..]) => first,
            ("youtube.com", ["watch"]) => QueryValue(link.Query, "v"),
            ("youtube.com", ["shorts" or "live" or "embed", string second, ..]) => second,
            _ => null,
        };
        return IsVideoId(id) ? id : null;
    }

    private static string? QueryValue(string query, string name) =>
        query
            .TrimStart('?')
            .Split('&')
            .Select(pair => pair.Split('=', 2))
            .Where(pair => pair.Length == 2 && pair[0] == name)
            .Select(pair => Uri.UnescapeDataString(pair[1]))
            .FirstOrDefault();

    [GeneratedRegex("^[A-Za-z0-9_-]{11}\\z")]
    private static partial Regex VideoId();

    [GeneratedRegex("^UC[A-Za-z0-9_-]{22}\\z")]
    private static partial Regex ChannelId();
}
