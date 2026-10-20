namespace TonTuyau.Application.Library;

/// <summary>Which of a channel's tabs on YouTube TonTuyau downloads (2.0, B4): its videos, its shorts, its streams.</summary>
public sealed record ChannelTabs(bool Videos, bool Shorts, bool Streams)
{
    /// <summary>The three: what "Add a channel" ticks by default (question 15), and what TubeArchivist's become.</summary>
    public static readonly ChannelTabs All = new(Videos: true, Shorts: true, Streams: true);

    /// <summary>At least one: a subscription to none would download nothing.</summary>
    public bool Any => Videos || Shorts || Streams;

    /// <summary>The tabs named, <c>videos,shorts,streams</c> or some of them, in any order.</summary>
    /// <returns>Null when a name isn't one of them, or none is given.</returns>
    public static ChannelTabs? Parse(string names)
    {
        ArgumentNullException.ThrowIfNull(names);
        string[] given = names.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (given.Length == 0 || given.Any(n => n is not ("videos" or "shorts" or "streams")))
        {
            return null;
        }

        return new ChannelTabs(given.Contains("videos"), given.Contains("shorts"), given.Contains("streams"));
    }
}
