using Elastic.Clients.Elasticsearch;
using Elastic.Clients.Elasticsearch.Aggregations;
using Elastic.Clients.Elasticsearch.Core.MGet;
using Elastic.Clients.Elasticsearch.Core.Search;
using Elastic.Clients.Elasticsearch.QueryDsl;
using TonTuyau.Application.Paging;
using TonTuyau.Application.Store;
using TonTuyau.Infrastructure.Elasticsearch.Documents;

namespace TonTuyau.Infrastructure.Elasticsearch;

// Browsing and search: the typed queries behind the REST API. They read the read alias (what users see).

internal sealed partial class VideoRepository
{
    /// <summary>Duration filter bounds, in seconds (API_CONTRACT.md: short &lt; 4 min, medium 4–20, long &gt; 20).</summary>
    public const double ShortUnder = 4 * 60;
    public const double LongOver = 20 * 60;

    /// <summary>Suggestions read this many hits per suggestion asked for, to have enough after removing duplicates.</summary>
    public const int SuggestOverfetch = 3;

    private static readonly Fields ListFields = Infer.Fields<VideoListDocument>(
        v => v.Id,
        v => v.ChannelId,
        v => v.ChannelName,
        v => v.Title,
        v => v.Type,
        v => v.Active,
        v => v.Published,
        v => v.DurationSeconds,
        v => v.Views,
        v => v.ThumbnailPath,
        v => v.ThumbnailArt!);

    /// <summary>
    /// Searched fields, French analysis + English sub-field (M2). The title counts most, then the channel name and
    /// tags, then the description.
    /// </summary>
    private static readonly Field[] SearchFields =
    [
        Infer.Field<VideoDocument>(v => v.Title, 3),
        Infer.Field<VideoDocument>(v => v.Title.Suffix("en"), 3),
        Infer.Field<VideoDocument>(v => v.ChannelName, 2),
        Infer.Field<VideoDocument>(v => v.ChannelName.Suffix("en"), 2),
        Infer.Field<VideoDocument>(v => v.Tags, 1.5f),
        Infer.Field<VideoDocument>(v => v.Description),
        Infer.Field<VideoDocument>(v => v.Description.Suffix("en")),
    ];

    /// <summary>Where the last word being typed may be the start of a word (QA10 M5-1): titles first.</summary>
    /// <summary>
    /// Where the last word being typed may be the start of a word (QA10 M5-1, QA11 M5-13), with the whole-word weights
    /// (M5-14): title, channel name, tags, then description.
    /// </summary>
    private static readonly PrefixField[] PrefixFields =
    [
        new(Infer.Field<VideoDocument>(v => v.Title.Suffix("autocomplete")), 3),
        new(Infer.Field<VideoDocument>(v => v.ChannelName.Suffix("autocomplete")), 2),
        new(Infer.Field<VideoDocument>(v => v.Tags), 1.5f, IndexedPrefixes: true),
        new(Infer.Field<VideoDocument>(v => v.Description.Suffix("prefix")), 1, IndexedPrefixes: true),
    ];

    public async Task<StoredVideo?> GetStoredAsync(string id, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(id);
        GetResponse<VideoDocument> response = (await Client
            .GetAsync<VideoDocument>(new GetRequest(ReadAlias, id), cancellationToken)
            .ConfigureAwait(false))
            .Ensure($"Get {ReadAlias}/{id}", 404);
        return response.Found && response.Source is not null
            ? new StoredVideo(
                DocumentMapper.ToVideo(response.Source),
                response.Source.MediaMissing == true,
                DocumentMapper.ToArt(response.Source.ThumbnailArt))
            : null;
    }

    public Task<StorePage<VideoListItem>> FindAsync(
        VideoQuery query,
        PageRequest page,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        return FindPageAsync<VideoListDocument, VideoListItem>(
            QueryFor(query),
            SortFor(query.Sort),
            page,
            ListFields,
            ToListItem,
            cancellationToken);
    }

    public async Task<IReadOnlyDictionary<string, TypeCounts>> CountByTypeAsync(
        IReadOnlyCollection<string> channelIds,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(channelIds);
        if (channelIds.Count == 0)
        {
            return new Dictionary<string, TypeCounts>();
        }

        const string ByChannel = "by_channel";
        const string ByType = "by_type";
        SearchRequest<VideoDocument> request = new(ReadAlias)
        {
            Size = 0,
            Query = InChannels(channelIds),
            Aggregations = new Dictionary<string, Aggregation>
            {
                [ByChannel] = new Aggregation
                {
                    Terms = new TermsAggregation
                    {
                        Field = Infer.Field<VideoDocument>(v => v.ChannelId),
                        Size = channelIds.Count,
                    },
                    Aggregations = new Dictionary<string, Aggregation>
                    {
                        [ByType] = new TermsAggregation
                        {
                            Field = Infer.Field<VideoDocument>(v => v.Type),
                            Size = Enum.GetValues<Application.Library.VideoType>().Length,
                        },
                    },
                },
            },
        };
        SearchResponse<VideoDocument> response = (await Client
            .SearchAsync<VideoDocument>(request, cancellationToken)
            .ConfigureAwait(false))
            .Ensure($"Count by type in {ReadAlias}");

        Dictionary<string, TypeCounts> counts = new(StringComparer.Ordinal);
        StringTermsAggregate channels = response.Aggregations?.GetStringTerms(ByChannel)
            ?? throw new SearchStoreException($"Count by type in {ReadAlias}: no '{ByChannel}' aggregation.");
        foreach (StringTermsBucket channel in channels.Buckets)
        {
            Dictionary<string, long> byType = (channel.Aggregations?.GetStringTerms(ByType)?.Buckets ?? [])
                .ToDictionary(b => (string)b.Key.Value!, b => b.DocCount, StringComparer.Ordinal);
            counts[(string)channel.Key.Value!] = new TypeCounts(
                byType.GetValueOrDefault(DocumentMapper.TypeName(Application.Library.VideoType.Video)),
                byType.GetValueOrDefault(DocumentMapper.TypeName(Application.Library.VideoType.Short)),
                byType.GetValueOrDefault(DocumentMapper.TypeName(Application.Library.VideoType.Stream)));
        }

        return counts;
    }

    public async Task<IReadOnlyList<TextMatch>> SuggestTitlesAsync(
        string prefix,
        int size,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(prefix);
        IReadOnlyList<TitleDocument> hits = await FindTopAsync<TitleDocument>(
            PrefixQuery(Infer.Field<VideoDocument>(v => v.Title.Suffix("autocomplete")), prefix),
            [SortKey.Score(), SortKey.By<VideoDocument>(v => v.Views, SortOrder.Desc, SortKeyKind.Integer)],
            size * SuggestOverfetch,
            Infer.Fields<TitleDocument>(t => t.Id, t => t.Title),
            cancellationToken)
            .ConfigureAwait(false);

        // Re-uploads and series share titles: one suggestion per title (the best match / most viewed one).
        return hits
            .DistinctBy(h => h.Title, StringComparer.OrdinalIgnoreCase)
            .Take(size)
            .Select(h => new TextMatch(h.Id, h.Title))
            .ToArray();
    }

    /// <summary>
    /// As-you-type on an <c>.autocomplete</c> sub-field (edge n-grams when indexing, folded words when searching):
    /// every word typed starts a word of the text, accents and case ignored. The typed words in the same order
    /// (a phrase on the n-grams) rank first: "tom c" puts "Tom Cooks" before "Cipher's Tomfoolery".
    /// </summary>
    internal static BoolQuery PrefixQuery(Field field, string prefix) => new()
    {
        Must = [new MatchQuery { Field = field, Query = prefix, Operator = Operator.And }],
        Should = [new MatchPhraseQuery { Field = field, Query = prefix, Boost = 2 }],
    };

    internal static Query QueryFor(VideoQuery query)
    {
        List<Query> filters = [];
        VideoFilter filter = query.Filter;
        if (filter.Type is not null)
        {
            filters.Add(new TermQuery
            {
                Field = Infer.Field<VideoDocument>(v => v.Type),
                Value = DocumentMapper.TypeName(filter.Type.Value),
            });
        }

        if (filter.ChannelId is not null)
        {
            filters.Add(new TermQuery
            {
                Field = Infer.Field<VideoDocument>(v => v.ChannelId),
                Value = filter.ChannelId,
            });
        }

        if (filter.PublishedFrom is not null)
        {
            filters.Add(new DateRangeQuery
            {
                Field = Infer.Field<VideoDocument>(v => v.Published),
                Gte = DateMath.Anchored(filter.PublishedFrom.Value.UtcDateTime),
            });
        }

        if (filter.Duration is not null)
        {
            filters.Add(DurationQuery(filter.Duration.Value));
        }

        BoolQuery result = new() { Filter = filters };
        if (filter.ExcludeId is not null)
        {
            result.MustNot =
            [
                new TermQuery { Field = Infer.Field<VideoDocument>(v => v.Id), Value = filter.ExcludeId },
            ];
        }

        if (query.Text is not null)
        {
            // Every word must be found, each in any of the fields; the last one may be the start of a word.
            result.Must = SearchTextQueries.Must(query.Text, SearchFields, PrefixFields);

            // The words as a phrase in the title rank first.
            result.Should =
            [
                new MatchPhraseQuery
                {
                    Field = Infer.Field<VideoDocument>(v => v.Title),
                    Query = query.Text.AllWords,
                    Boost = 2,
                },
            ];
        }

        return result;
    }

    internal static IReadOnlyList<SortKey> SortFor(VideoListSort sort) => sort switch
    {
        VideoListSort.Latest =>
        [
            SortKey.By<VideoDocument>(v => v.Published, SortOrder.Desc, SortKeyKind.Integer),
            SortKey.Id<VideoDocument>(),
        ],
        VideoListSort.Popular =>
        [
            SortKey.By<VideoDocument>(v => v.Views, SortOrder.Desc, SortKeyKind.Integer),
            SortKey.Id<VideoDocument>(),
        ],
        VideoListSort.Oldest =>
        [
            SortKey.By<VideoDocument>(v => v.Published, SortOrder.Asc, SortKeyKind.Integer),
            SortKey.Id<VideoDocument>(),
        ],
        VideoListSort.Relevance => [SortKey.Score(), SortKey.Id<VideoDocument>()],
        _ => throw new ArgumentOutOfRangeException(nameof(sort)),
    };

    private static NumberRangeQuery DurationQuery(DurationRange duration)
    {
        Field field = Infer.Field<VideoDocument>(v => v.DurationSeconds);
        return duration switch
        {
            DurationRange.Short => new NumberRangeQuery(field) { Lt = ShortUnder },
            DurationRange.Medium => new NumberRangeQuery(field) { Gte = ShortUnder, Lte = LongOver },
            DurationRange.Long => new NumberRangeQuery(field) { Gt = LongOver },
            _ => throw new ArgumentOutOfRangeException(nameof(duration)),
        };
    }

    private static VideoListItem ToListItem(VideoListDocument d) => new(
        d.Id,
        d.ChannelId,
        d.ChannelName,
        d.Title,
        DocumentMapper.ParseType(d.Type),
        d.Active,
        d.Published,
        TimeSpan.FromSeconds(d.DurationSeconds),
        d.Views,
        d.ThumbnailPath,
        DocumentMapper.ToArt(d.ThumbnailArt));
}

internal sealed partial class ChannelRepository
{
    private static readonly Fields ListFields = Infer.Fields<ChannelListDocument>(
        c => c.Id,
        c => c.Name,
        c => c.Subscribers,
        c => c.ThumbPath!,
        c => c.ThumbArt!);

    private static readonly Field[] SearchFields =
    [
        Infer.Field<ChannelDocument>(c => c.Name, 3),
        Infer.Field<ChannelDocument>(c => c.Name.Suffix("en"), 3),
        Infer.Field<ChannelDocument>(c => c.Tags, 1.5f),
        Infer.Field<ChannelDocument>(c => c.Description),
        Infer.Field<ChannelDocument>(c => c.Description.Suffix("en")),
    ];

    /// <summary>Where the last word being typed may be the start of a word (QA10 M5-1, QA11 M5-13): name, tags, description.</summary>
    private static readonly PrefixField[] PrefixFields =
    [
        new(Infer.Field<ChannelDocument>(c => c.Name.Suffix("autocomplete")), 3),
        new(Infer.Field<ChannelDocument>(c => c.Tags), 1.5f, IndexedPrefixes: true),
        new(Infer.Field<ChannelDocument>(c => c.Description.Suffix("prefix")), 1, IndexedPrefixes: true),
    ];

    public async Task<StoredChannel?> GetStoredAsync(string id, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(id);
        GetResponse<ChannelDocument> response = (await Client
            .GetAsync<ChannelDocument>(new GetRequest(ReadAlias, id), cancellationToken)
            .ConfigureAwait(false))
            .Ensure($"Get {ReadAlias}/{id}", 404);
        return response.Found && response.Source is not null
            ? new StoredChannel(
                DocumentMapper.ToChannel(response.Source),
                DocumentMapper.ToArt(response.Source.ThumbArt),
                DocumentMapper.ToArt(response.Source.BannerArt))
            : null;
    }

    public Task<StorePage<ChannelListItem>> FindAsync(
        ChannelQuery query,
        PageRequest page,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        return FindPageAsync<ChannelListDocument, ChannelListItem>(
            QueryFor(query),
            SortFor(query.Sort),
            page,
            ListFields,
            ToListItem,
            cancellationToken);
    }

    public async Task<IReadOnlyDictionary<string, ChannelListItem>> GetListItemsAsync(
        IReadOnlyCollection<string> ids,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(ids);
        Dictionary<string, ChannelListItem> result = new(StringComparer.Ordinal);
        if (ids.Count == 0)
        {
            return result;
        }

        MultiGetRequest request = new(ReadAlias) { Ids = new Ids(ids), SourceIncludes = ListFields };
        MultiGetResponse<ChannelListDocument> response = (await Client
            .MultiGetAsync<ChannelListDocument>(request, cancellationToken)
            .ConfigureAwait(false))
            .Ensure($"Multi-get {ReadAlias}");
        foreach (MultiGetResponseItem<ChannelListDocument> item in response.Docs)
        {
            item.Match(
                found =>
                {
                    if (found is { Found: true, Source: { } d })
                    {
                        result[d.Id] = ToListItem(d);
                    }
                },
                error => throw new SearchStoreException(
                    $"Multi-get {ReadAlias}: {error?.Id}: {error?.Error?.Reason}"));
        }

        return result;
    }

    public async Task<IReadOnlyList<TextMatch>> SuggestNamesAsync(
        string prefix,
        int size,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(prefix);
        IReadOnlyList<NameDocument> hits = await FindTopAsync<NameDocument>(
            NamePrefixQuery(prefix),
            [SortKey.Score(), SortKey.By<ChannelDocument>(c => c.Subscribers, SortOrder.Desc, SortKeyKind.Integer)],
            size,
            Infer.Fields<NameDocument>(n => n.Id, n => n.Name),
            cancellationToken)
            .ConfigureAwait(false);
        return hits
            .Select(h => new TextMatch(h.Id, h.Name))
            .ToArray();
    }

    internal static Query QueryFor(ChannelQuery query)
    {
        BoolQuery result = new();
        if (query.NamePrefix is not null)
        {
            result.Filter = [NamePrefixQuery(query.NamePrefix)];
        }

        if (query.Text is not null)
        {
            result.Must = SearchTextQueries.Must(query.Text, SearchFields, PrefixFields);
        }

        return result;
    }

    internal static IReadOnlyList<SortKey> SortFor(ChannelListSort sort) => sort switch
    {
        ChannelListSort.Name =>
        [
            SortKey.By<ChannelDocument>(c => c.Name.Suffix("sort"), SortOrder.Asc, SortKeyKind.Text),
            SortKey.Id<ChannelDocument>(),
        ],
        ChannelListSort.Subscribers =>
        [
            SortKey.By<ChannelDocument>(c => c.Subscribers, SortOrder.Desc, SortKeyKind.Integer),
            SortKey.Id<ChannelDocument>(),
        ],
        ChannelListSort.Relevance => [SortKey.Score(), SortKey.Id<ChannelDocument>()],
        _ => throw new ArgumentOutOfRangeException(nameof(sort)),
    };

    /// <summary>Every word typed starts a word of the name (see <see cref="VideoRepository.PrefixQuery"/>).</summary>
    private static BoolQuery NamePrefixQuery(string prefix) =>
        VideoRepository.PrefixQuery(Infer.Field<ChannelDocument>(c => c.Name.Suffix("autocomplete")), prefix);

    private static ChannelListItem ToListItem(ChannelListDocument d) =>
        new(d.Id, d.Name, d.Subscribers, d.ThumbPath, DocumentMapper.ToArt(d.ThumbArt));
}
