using Elastic.Clients.Elasticsearch;
using Elastic.Clients.Elasticsearch.Aggregations;
using Elastic.Clients.Elasticsearch.Core.Search;
using Elastic.Clients.Elasticsearch.QueryDsl;
using TonTuyau.Application.Dashboard;
using TonTuyau.Application.Library;
using TonTuyau.Application.Store;
using TonTuyau.Application.Sync;
using TonTuyau.Infrastructure.Elasticsearch.Documents;

namespace TonTuyau.Infrastructure.Elasticsearch;

/// <summary>
/// The admin dashboard's reads: the read aliases (what users see) and the run history. The library is counted by
/// aggregations: no video is loaded.
/// </summary>
internal sealed class DashboardRepository(ElasticsearchClient client, IndexNames names) : IDashboardRepository
{
    /// <summary>The most hits one search gives (Elasticsearch's default <c>max_result_window</c>).</summary>
    public const int MaxChannels = 10_000;

    private const string ByType = "by_type";
    private const string ByChannel = "by_channel";
    private const string Duration = "duration";
    private const string Gone = "gone";

    private static readonly Fields ChannelFields = Infer.Fields<ChannelListDocument>(
        c => c.Id,
        c => c.Name,
        c => c.Subscribers,
        c => c.ThumbPath!,
        c => c.ThumbArt!);

    private static readonly Query GoneQuery =
        new TermQuery { Field = Infer.Field<VideoDocument>(v => v.Active), Value = false };

    private string Videos => names.ReadAlias(StoreIndex.Videos);

    private string Channels => names.ReadAlias(StoreIndex.Channels);

    public async Task<IReadOnlyList<ChannelListItem>> GetChannelsAsync(CancellationToken cancellationToken)
    {
        // One search: a library has a few hundred channels. Past the window, say so rather than show some of them.
        SearchRequest<ChannelDocument> request = new(Channels)
        {
            Size = MaxChannels,
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
                $"List {Channels}: {response.Total} channels, the dashboard lists {MaxChannels} at most.");
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
                    channel.Aggregations?.GetFilter(Gone)?.DocCount ?? 0);
            }
        }

        return new LibraryStats(byType, result.GetFilter(Gone)?.DocCount ?? 0, byChannel);
    }

    public async Task<IReadOnlyList<SyncRun>> GetRecentRunsAsync(int count, CancellationToken cancellationToken)
    {
        SearchRequest<SyncRun> request = new(names.SyncRuns)
        {
            Size = count,
            Sort = [new SortOptions {
                Field = new FieldSort { Field = Infer.Field<SyncRun>(r => r.StartedAt), Order = SortOrder.Desc } }],

            // The notes and rejected samples are most of a run's size, and the dashboard reads hundreds of runs.
            Source = new SourceConfig(new SourceFilter
            {
                Excludes = Infer.Fields<SyncRun>(r => r.Notes, r => r.RejectedSamples),
            }),
        };
        SearchResponse<SyncRun> response = (await client
            .SearchAsync<SyncRun>(request, cancellationToken)
            .ConfigureAwait(false))
            .Ensure($"Search {names.SyncRuns}");
        return response.Hits
            .Select(h => h.Source)
            .OfType<SyncRun>()
            .ToArray();
    }

    private static IReadOnlyCollection<StringTermsBucket> Buckets(AggregateDictionary? aggregations, string name) =>
        aggregations?.GetStringTerms(name)?.Buckets
            ?? throw new SearchStoreException($"Count the library: no '{name}' aggregation.");
}
