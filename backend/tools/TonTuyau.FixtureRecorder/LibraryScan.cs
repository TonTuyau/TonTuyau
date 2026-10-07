using System.Diagnostics;
using System.Globalization;
using TonTuyau.Application.Library;
using TonTuyau.Application.TubeArchivist;
using TonTuyau.TubeArchivist;
using TonTuyau.TubeArchivist.Validation;

namespace TonTuyau.FixtureRecorder;

/// <summary>
/// Dev tool, read-only: walks the whole library through the real client (channel by channel, so no list goes past
/// TA's 10k window) and reports what the validators reject. Prints to the console only; writes nothing.
/// Not the sync: no state, no retries, no ordering guarantees.
/// </summary>
internal static class LibraryScan
{
    public static async Task<int> RunAsync(HttpClient http)
    {
        TubeArchivistGateway gateway = new(http, new VideoDtoValidator(), new ChannelDtoValidator());
        CancellationToken ct = CancellationToken.None;
        Stopwatch clock = Stopwatch.StartNew();
        int requests = 0;

        LibraryStats stats = await gateway.GetLibraryStatsAsync(ct);
        requests++;

        List<Channel> channels = new();
        List<RejectedItem> rejected = new();
        for (int page = 1; ; page++)
        {
            Page<Channel> result = await gateway.GetChannelPageAsync(page, ct);
            requests++;
            if (result.IsPastEnd)
            {
                break;
            }

            channels.AddRange(result.Items);
            rejected.AddRange(result.Rejected);
        }

        List<(string Id, string Name, int Count)> perChannel = new();
        HashSet<string> seen = new(StringComparer.Ordinal);
        int duplicates = 0;
        long valid = 0;
        List<string> corrected = new();
        foreach (Channel channel in channels)
        {
            int count = 0;
            for (int page = 1; ; page++)
            {
                Page<Video> result = await gateway.GetVideoPageAsync(
                    new VideoPageQuery(VideoSort.Published, SortOrder.Asc, channel.Id),
                    page,
                    ct);
                requests++;
                if (result.IsPastEnd)
                {
                    break;
                }

                foreach (Video video in result.Items)
                {
                    if (video.MediaPathCorrected)
                    {
                        corrected.Add(video.Id);
                    }

                    if (!seen.Add(video.Id))
                    {
                        duplicates++;
                    }
                }

                count += result.Items.Count + result.Rejected.Count;
                valid += result.Items.Count;
                rejected.AddRange(result.Rejected);
            }

            perChannel.Add((channel.Id, channel.Name, count));
        }

        Console.WriteLine($"TA stats total     : {stats.Total:N0} videos");
        Console.WriteLine($"Channels           : {channels.Count:N0}");
        int rejectedVideos = rejected.Count(r => r.Subject.StartsWith("TubeArchivist video", StringComparison.Ordinal));
        Console.WriteLine($"Videos walked      : {valid + rejectedVideos:N0} ({valid:N0} valid)");
        Console.WriteLine($"Duplicate video ids: {duplicates:N0}");
        IEnumerable<string> biggest = perChannel
            .OrderByDescending(c => c.Count)
            .Take(3)
            .Select(c => c.Count.ToString("N0", CultureInfo.CurrentCulture));
        Console.WriteLine($"Biggest channels   : {string.Join(", ", biggest)}");
        Console.WriteLine($"Requests           : {requests:N0} in {clock.Elapsed.TotalSeconds:N0} s");
        string examples = corrected.Count > 0 ? $" ({string.Join(", ", corrected.Take(10))})" : "";
        Console.WriteLine($"Media path corrected: {corrected.Count:N0}{examples}");
        Console.WriteLine($"Rejected           : {rejected.Count:N0}");
        IOrderedEnumerable<IGrouping<string, string>> errorGroups = rejected
            .SelectMany(r => r.Errors.Select(e => $"{e.Field}: {e.Message}"))
            .GroupBy(e => e)
            .OrderByDescending(g => g.Count());
        foreach (IGrouping<string, string>? group in errorGroups)
        {
            Console.WriteLine($"  {group.Count(),6:N0} × {group.Key}");
        }

        foreach (RejectedItem? r in rejected.Take(20))
        {
            Console.WriteLine($"  e.g. {r.Subject}: {string.Join("; ", r.Errors.Select(e => e.Field))}");
        }

        return 0;
    }
}
