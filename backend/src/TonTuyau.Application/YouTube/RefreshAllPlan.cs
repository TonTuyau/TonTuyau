namespace TonTuyau.Application.YouTube;

/// <summary>
/// The order of "Refresh everything" (2.0, B8): channel by channel, each channel then its videos, as "Refresh a channel
/// and its videos" reads them, the least recently read first. What TonTuyau read in the last days is skipped, so a run
/// started after a stop goes on with what's left (questions 27 to 29).
/// </summary>
public static class RefreshAllPlan
{
    /// <summary>The work: channel ids each followed by their videos' ids; and how many were skipped.</summary>
    /// <param name="checks">Every channel and video of the library (<see cref="IYouTubeFactsSource.GetAllChecksAsync"/>).</param>
    /// <param name="days">What was read in the last this many days is skipped; 0: nothing is.</param>
    public static (IReadOnlyList<string> Work, int Skipped) Order(
        IReadOnlyCollection<LibraryCheck> checks,
        DateTimeOffset now,
        int days)
    {
        ArgumentNullException.ThrowIfNull(checks);
        DateTimeOffset since = now.AddDays(-days);

        // Gone isn't read any more (1.5); a real answer read lately is left. Never checked, or to retry: read.
        bool Skip(LibraryCheck check) =>
            check.State == YouTubeState.Gone
            || (days > 0
                && check.CheckedAt >= since
                && check.State is YouTubeState.OnYouTube or YouTubeState.Unlisted or YouTubeState.Private);

        Dictionary<string, List<LibraryCheck>> videosOf = checks
            .Where(c => c.ChannelId is not null && !Skip(c))
            .GroupBy(c => c.ChannelId!, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.Ordinal);
        IEnumerable<Group> groups = checks
            .Where(c => c.ChannelId is null)
            .Select(channel => GroupOf(channel, !Skip(channel), videosOf.GetValueOrDefault(channel.Id) ?? []))
            .Where(g => g.ReadIt || g.Videos.Count > 0)
            .OrderBy(g => g.Oldest)
            .ThenBy(g => g.Channel.Id, StringComparer.Ordinal);
        List<string> work = [];
        foreach (Group group in groups)
        {
            if (group.ReadIt)
            {
                work.Add(group.Channel.Id);
            }

            work.AddRange(group.Videos
                .OrderBy(Read)
                .ThenBy(v => v.Id, StringComparer.Ordinal)
                .Select(v => v.Id));
        }

        return (work, checks.Count(Skip));
    }

    /// <summary>Never read (or to retry) is the oldest read of all.</summary>
    private static DateTimeOffset Read(LibraryCheck check) =>
        check.State is YouTubeState.NotChecked or YouTubeState.RetryLater || check.CheckedAt is null
            ? DateTimeOffset.MinValue
            : check.CheckedAt.Value;

    /// <summary>A channel to read, or not, and its videos to read; ordered by the least recently read of them.</summary>
    private static Group GroupOf(LibraryCheck channel, bool readIt, List<LibraryCheck> videos)
    {
        IEnumerable<LibraryCheck> items = readIt ? [channel, .. videos] : videos;
        return new Group(channel, readIt, videos, items.Select(Read).DefaultIfEmpty(DateTimeOffset.MaxValue).Min());
    }

    private sealed record Group(LibraryCheck Channel, bool ReadIt, List<LibraryCheck> Videos, DateTimeOffset Oldest);
}
