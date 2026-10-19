using Elastic.Clients.Elasticsearch;
using Elastic.Clients.Elasticsearch.Aggregations;
using Elastic.Clients.Elasticsearch.Core.Search;
using Elastic.Clients.Elasticsearch.QueryDsl;
using TonTuyau.Application.Dashboard;
using TonTuyau.Application.Library;
using TonTuyau.Application.Store;
using TonTuyau.Application.Sync;
using TonTuyau.Application.YouTube;
using TonTuyau.Infrastructure.Elasticsearch.Documents;
using TonTuyau.Infrastructure.Runs;

namespace TonTuyau.Infrastructure.Elasticsearch;

/// <summary>
/// The admin dashboard's reads: the read aliases (what users see), and the run history (2.0: PostgreSQL, through
/// <see cref="TaskRepository"/>). The library is counted by aggregations: no video is loaded.
/// </summary>
internal sealed class DashboardRepository(
    ElasticsearchClient client,
    IndexNames names,
    TaskRepository runs) : IDashboardRepository
{
    /// <summary>The most hits one search gives (Elasticsearch's default <c>max_result_window</c>).</summary>
    public const int MaxHits = 10_000;

    private const string ByType = "by_type";
    private const string ByChannel = "by_channel";
    private const string Duration = "duration";
    private const string Gone = "gone";
    private const string Private = "private";
    private const string Unlisted = "unlisted";
    private const string RetryLater = "retry_later";
    private const string NotChecked = "not_checked";

    private static readonly Fields ChannelFields = Infer.Fields<ChannelListDocument>(
        c => c.Id,
        c => c.Name,
        c => c.Subscribers,
        c => c.ThumbPath!,
        c => c.ThumbArt!);

    private static readonly Query GoneQuery = StateQuery(YouTubeState.Gone);

    private static readonly Query PrivateQuery = StateQuery(YouTubeState.Private);

    private static readonly Query UnlistedQuery = StateQuery(YouTubeState.Unlisted);

    private static readonly Query RetryLaterQuery = StateQuery(YouTubeState.RetryLater);

    private static readonly Query NotCheckedQuery = StateQuery(YouTubeState.NotChecked);

    private string Videos => names.ReadAlias(StoreIndex.Videos);

    private string Channels => names.ReadAlias(StoreIndex.Channels);

    public async Task<IReadOnlyList<ChannelListItem>> GetChannelsAsync(CancellationToken cancellationToken)
    {
        // One search: a library has a few hundred channels. Past the window, say so rather than show some of them.
        SearchRequest<ChannelDocument> request = new(Channels)
        {
            Size = MaxHits,
            TrackTotalHits = new TrackHits(true),
            Source = new SourceConfig(new SourceFilter { Includes = ChannelFields }),
        };
        SearchResponse<ChannelListDocument> response = (await client
            .SearchAsync<ChannelListDocument>(request, cancellationToken)
            .ConfigureAwait(false))
            .Ensure($"List {Channels}");
        if (response.Total > response.Hits.Count)
        {
            throw new SearchStoreException(
                $"List {Channels}: {response.Total} channels, the dashboard lists {MaxHits} at most.");
        }

        return response.Hits
            .Select(h => h.Source)
            .OfType<ChannelListDocument>()
            .Select(d => new ChannelListItem(
                d.Id,
                d.Name,
                d.Subscribers,
                d.ThumbPath,
                DocumentMapper.ToArt(d.ThumbArt)))
            .ToArray();
    }

    public async Task<LibraryStats> GetLibraryStatsAsync(
        IReadOnlyCollection<string> channelIds,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(channelIds);
        int types = Enum.GetValues<VideoType>().Length;
        Dictionary<string, Aggregation> aggregations = new()
        {
            [ByType] = new Aggregation
            {
                Terms = new TermsAggregation { Field = Infer.Field<VideoDocument>(v => v.Type), Size = types },
                Aggregations = new Dictionary<string, Aggregation>
                {
                    [Duration] = new SumAggregation { Field = Infer.Field<VideoDocument>(v => v.DurationSeconds) },
                },
            },
            [Gone] = new Aggregation { Filter = GoneQuery },
        };
        if (channelIds.Count > 0)
        {
            aggregations[ByChannel] = new Aggregation
            {
                Terms = new TermsAggregation
                {
                    Field = Infer.Field<VideoDocument>(v => v.ChannelId),
                    Include = new TermsInclude(channelIds),
                    Size = channelIds.Count,
                },
                Aggregations = new Dictionary<string, Aggregation>
                {
                    [ByType] = new TermsAggregation { Field = Infer.Field<VideoDocument>(v => v.Type), Size = types },
                    [Gone] = new Aggregation { Filter = GoneQuery },
                    [Private] = new Aggregation { Filter = PrivateQuery },
                    [Unlisted] = new Aggregation { Filter = UnlistedQuery },
                    [RetryLater] = new Aggregation { Filter = RetryLaterQuery },
                    [NotChecked] = new Aggregation { Filter = NotCheckedQuery },
                },
            };
        }

        SearchRequest<VideoDocument> request = new(Videos) { Size = 0, Aggregations = aggregations };
        SearchResponse<VideoDocument> response = (await client
            .SearchAsync<VideoDocument>(request, cancellationToken)
            .ConfigureAwait(false))
            .Ensure($"Count the library in {Videos}");
        AggregateDictionary result = response.Aggregations
            ?? throw new SearchStoreException($"Count the library in {Videos}: no aggregations.");

        TypeStats[] byType = Buckets(result, ByType)
            .Select(b => new TypeStats(
                DocumentMapper.ParseType((string)b.Key.Value!),
                b.DocCount,
                TimeSpan.FromSeconds(b.Aggregations?.GetSum(Duration)?.Value ?? 0)))
            .ToArray();
        Dictionary<string, ChannelStats> byChannel = new(StringComparer.Ordinal);
        if (channelIds.Count > 0)
        {
            foreach (StringTermsBucket channel in Buckets(result, ByChannel))
            {
                Dictionary<string, long> counts = Buckets(channel.Aggregations, ByType)
                    .ToDictionary(b => (string)b.Key.Value!, b => b.DocCount, StringComparer.Ordinal);
                byChannel[(string)channel.Key.Value!] = new ChannelStats(
                    new TypeCounts(
                        counts.GetValueOrDefault(DocumentMapper.TypeName(VideoType.Video)),
                        counts.GetValueOrDefault(DocumentMapper.TypeName(VideoType.Short)),
                        counts.GetValueOrDefault(DocumentMapper.TypeName(VideoType.Stream))),
                    channel.Aggregations?.GetFilter(Gone)?.DocCount ?? 0,
                    channel.Aggregations?.GetFilter(Private)?.DocCount ?? 0,
                    channel.Aggregations?.GetFilter(Unlisted)?.DocCount ?? 0,
                    channel.Aggregations?.GetFilter(RetryLater)?.DocCount ?? 0,
                    channel.Aggregations?.GetFilter(NotChecked)?.DocCount ?? 0);
            }
        }

        return new LibraryStats(byType, result.GetFilter(Gone)?.DocCount ?? 0, byChannel);
    }

    public Task<SyncRun?> GetLatestSuccessfulRunAsync(CancellationToken cancellationToken) =>
        runs.GetLatestSuccessfulSyncAsync(cancellationToken);

    public Task<IReadOnlyList<SyncRun>> GetRunsSinceAsync(DateTimeOffset? since, CancellationToken cancellationToken) =>
        runs.GetSyncsSinceAsync(since, cancellationToken);

    private static IReadOnlyCollection<StringTermsBucket> Buckets(AggregateDictionary? aggregations, string name) =>
        aggregations?.GetStringTerms(name)?.Buckets
            ?? throw new SearchStoreException($"Count the library: no '{name}' aggregation.");

    private static TermQuery StateQuery(YouTubeState state) => new()
    {
        Field = Infer.Field<VideoDocument>(v => v.YoutubeState),
        Value = DocumentMapper.StateName(state),
    };
}
