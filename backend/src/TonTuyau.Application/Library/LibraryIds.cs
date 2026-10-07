using System.Text.RegularExpressions;

namespace TonTuyau.Application.Library;

/// <summary>YouTube id formats, as TubeArchivist stores them. Ids from URLs are checked against these first.</summary>
public static partial class LibraryIds
{
    public static bool IsVideoId(string? id) => id is not null && VideoId().IsMatch(id);

    public static bool IsChannelId(string? id) => id is not null && ChannelId().IsMatch(id);

    [GeneratedRegex("^[A-Za-z0-9_-]{11}\\z")]
    private static partial Regex VideoId();

    [GeneratedRegex("^UC[A-Za-z0-9_-]{22}\\z")]
    private static partial Regex ChannelId();
}
