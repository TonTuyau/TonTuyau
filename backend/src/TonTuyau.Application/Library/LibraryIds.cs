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

    /// <summary>
    /// A channel from what an admin pastes (2.0, "Add a channel"): its id, its <c>@handle</c>, or a YouTube link to
    /// either (<c>youtube.com/channel/UC…</c>, <c>youtube.com/@name</c>, a tab of it like <c>/videos</c> included).
    /// </summary>
    /// <returns>Its id (<c>UC…</c>) or its handle (<c>@name</c>); null when it's neither.</returns>
    public static string? ChannelTargetOf(string? text)
    {
        string trimmed = text?.Trim() ?? "";
        if (IsChannelId(trimmed) || IsHandle(trimmed))
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
            : host;
        string[] path = link.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        string? target = (host, path) switch
        {
            ("youtube.com", ["channel", string id, ..]) => id,
            ("youtube.com", [string handle, ..]) when handle.StartsWith('@') => Uri.UnescapeDataString(handle),
            _ => null,
        };
        return IsChannelId(target) || IsHandle(target) ? target : null;
    }

    /// <summary>A YouTube handle, <c>@</c> and 3 to 30 letters, digits, <c>_</c>, <c>-</c> or <c>.</c>.</summary>
    public static bool IsHandle(string? text) => text is not null && Handle().IsMatch(text);

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

    [GeneratedRegex("^@[A-Za-z0-9_.-]{3,30}\\z")]
    private static partial Regex Handle();
}
