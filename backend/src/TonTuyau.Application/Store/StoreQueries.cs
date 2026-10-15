using System.Diagnostics.CodeAnalysis;
using TonTuyau.Application.Library;

namespace TonTuyau.Application.Store;

public enum VideoListSort
{
    /// <summary>Published, newest first.</summary>
    Latest,

    /// <summary>Views, most first.</summary>
    Popular,

    /// <summary>Published, oldest first.</summary>
    Oldest,

    /// <summary>Best match first (searches only).</summary>
    Relevance,
}

public enum ChannelListSort
{
    /// <summary>Name, A to Z (accents and case ignored).</summary>
    Name,

    /// <summary>Subscribers, most first.</summary>
    Subscribers,

    /// <summary>Best match first (searches only).</summary>
    Relevance,
}

/// <summary>The search's upload-date filter: published within the last day, week, month or year.</summary>
public enum PublishedPeriod
{
    /// <summary>The last 24 hours.</summary>
    Today,

    /// <summary>The last 7 days.</summary>
    Week,

    /// <summary>The last 30 days.</summary>
    Month,

    /// <summary>The last 365 days.</summary>
    Year,
}

/// <summary>Duration buckets of the search filter.</summary>
[SuppressMessage("Naming", "CA1720:Identifier contains type name", Justification = "The API's duration values.")]
public enum DurationRange
{
    /// <summary>Under 4 minutes.</summary>
    Short,

    /// <summary>4 to 20 minutes, both included.</summary>
    Medium,

    /// <summary>Over 20 minutes.</summary>
    Long,
}

/// <param name="PublishedFrom">Published at or after this instant (a search's <c>published</c> cut-off).</param>
/// <param name="ExcludeId">A video left out (of the items and of the total).</param>
public sealed record VideoFilter(
    VideoType? Type = null,
    string? ChannelId = null,
    DateTimeOffset? PublishedFrom = null,
    DurationRange? Duration = null,
    string? ExcludeId = null)
{
    public static readonly VideoFilter None = new();
}

/// <summary>
/// A search's text as words (QA10 M5-1): every word must be found (AND, M4-Q1), each as a whole word, except the
/// last one while it's still being typed, which may also be the start of a word ("creme b" finds "Crème brûlée").
/// </summary>
/// <param name="Words">Whole words, in the order typed.</param>
/// <param name="Prefix">The last word when the text doesn't end with a space: a whole word or the start of one.</param>
public sealed record SearchText(IReadOnlyList<string> Words, string? Prefix)
{
    public static readonly SearchText Empty = new([], null);

    /// <summary>True when nothing searchable was typed (only spaces or punctuation): nothing matches.</summary>
    public bool IsEmpty => Words.Count == 0 && Prefix is null;

    /// <summary>Every word, the prefix last, as typed (for phrase matching).</summary>
    public string AllWords => string.Join(' ', Prefix is null ? Words : [.. Words, Prefix]);

    /// <summary>
    /// Splits the text on white space. A trailing space means the last word is complete; a "word" without any letter
    /// or digit (a lone dash, an emoji) is left out, as the analysers would.
    /// </summary>
    public static SearchText Parse(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        string[] tokens = text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        bool typing = tokens.Length > 0 && !char.IsWhiteSpace(text[^1]) && IsWord(tokens[^1]);
        string[] words = tokens
            .Take(typing ? tokens.Length - 1 : tokens.Length)
            .Where(IsWord)
            .ToArray();
        return new SearchText(words, typing ? tokens[^1] : null);
    }

    public bool Equals(SearchText? other) =>
        other is not null && Words.SequenceEqual(other.Words, StringComparer.Ordinal) && Prefix == other.Prefix;

    public override int GetHashCode() => HashCode.Combine(Words.Count, Prefix);

    private static bool IsWord(string token) => token.Any(char.IsLetterOrDigit);
}

/// <param name="Text">What to search for in titles, channel names, tags and descriptions; null for a plain list.</param>
public sealed record VideoQuery(SearchText? Text, VideoFilter Filter, VideoListSort Sort);

/// <param name="NamePrefix">As-you-type filter on the name: every word typed starts a word of the name.</param>
/// <param name="Text">What to search for in names, tags and descriptions.</param>
public sealed record ChannelQuery(string? NamePrefix, SearchText? Text, ChannelListSort Sort);

/// <summary>What a list needs to know about a video (no description, no tracks).</summary>
/// <param name="Active">False only when TonTuyau confirmed the video is gone from YouTube (1.5).</param>
/// <param name="ThumbnailArt">The thumbnail's versions as the sync recorded them; null when not recorded (yet).</param>
public sealed record VideoListItem(
    string Id,
    string ChannelId,
    string ChannelName,
    string Title,
    VideoType Type,
    bool Active,
    DateTimeOffset Published,
    TimeSpan Duration,
    long Views,
    string ThumbnailPath,
    ArtVersions? ThumbnailArt = null);

/// <summary>What a list needs to know about a channel.</summary>
public sealed record ChannelListItem(
    string Id,
    string Name,
    long Subscribers,
    string? ThumbPath,
    ArtVersions? ThumbArt = null);

/// <summary>A stored video with the sync's "file missing on the share" flag and its thumbnail's recorded versions.</summary>
public sealed record StoredVideo(Video Video, bool MediaMissing, ArtVersions? ThumbnailArt = null);

/// <summary>A stored channel with its pictures' recorded versions (null when not recorded yet).</summary>
public sealed record StoredChannel(Channel Channel, ArtVersions? ThumbArt, ArtVersions? BannerArt);

/// <summary>How many videos of each type a channel has in the store.</summary>
public sealed record TypeCounts(long Videos, long Shorts, long Streams)
{
    public static readonly TypeCounts None = new(0, 0, 0);
}

/// <summary>A suggestion as the store finds it: the id and the text that matched (a name or a title).</summary>
public sealed record TextMatch(string Id, string Text);
