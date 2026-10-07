using Elastic.Clients.Elasticsearch;
using Elastic.Clients.Elasticsearch.QueryDsl;
using TonTuyau.Application.Library;
using TonTuyau.Application.Store;
using TonTuyau.Infrastructure.Elasticsearch.Documents;

namespace TonTuyau.Infrastructure.Elasticsearch;

internal sealed partial class VideoRepository(ElasticsearchClient client, IndexNames names)
    : DocumentRepository<Video, VideoDocument>(client, names, StoreIndex.Videos), IVideoRepository
{
    private static readonly Fields SummaryFields = Infer.Fields<VideoSummaryDocument>(
        v => v.Id,
        v => v.Title,
        v => v.ChannelId,
        v => v.Type,
        v => v.Downloaded,
        v => v.SeenInRun!,
        v => v.ThumbnailPath);

    public Task<IReadOnlyList<VideoSummary>> FindNotSeenAsync(
        string runId,
        IReadOnlyCollection<string> channelIds,
        CancellationToken cancellationToken)
    {
        BoolQuery query = new()
        {
            Filter = [InChannels(channelIds)],
            MustNot = [new TermQuery { Field = Infer.Field<VideoDocument>(v => v.SeenInRun!), Value = runId }],
        };
        return SummariesAsync(query, cancellationToken);
    }

    public Task<IReadOnlyList<VideoSummary>> FindByChannelsAsync(
        IReadOnlyCollection<string> channelIds,
        CancellationToken cancellationToken) =>
        SummariesAsync(new BoolQuery { Filter = [InChannels(channelIds)] }, cancellationToken);

    public async Task<IReadOnlySet<string>> GetMediaMissingIdsAsync(CancellationToken cancellationToken)
    {
        TermQuery flagged = new() { Field = Infer.Field<VideoDocument>(v => v.MediaMissing!), Value = true };
        IReadOnlyList<IdDocument> hits = await SearchAllAsync<IdDocument>(
            flagged,
            Infer.Fields<IdDocument>(d => d.Id),
            cancellationToken)
            .ConfigureAwait(false);
        return hits
            .Select(h => h.Id)
            .ToHashSet(StringComparer.Ordinal);
    }

    public Task SetMediaMissingAsync(IReadOnlyCollection<string> ids, bool missing, CancellationToken cancellationToken)
        =>
        PartialUpdateAsync(ids, new MediaMissingPartial(missing), cancellationToken);

    protected override VideoDocument ToDocument(Video item) => DocumentMapper.ToDocument(item);

    protected override Video ToDomain(VideoDocument document) => DocumentMapper.ToVideo(document);

    public Task SetThumbnailArtAsync(IReadOnlyDictionary<string, ArtVersions> art, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(art);
        return PartialUpdateEachAsync(
            art.Select(a => KeyValuePair.Create(a.Key, new ThumbnailArtPartial(DocumentMapper.ToDocument(a.Value)))),
            cancellationToken);
    }

    protected override VideoDocument WithBookkeeping(
        VideoDocument document,
        string? seenInRun,
        StoreBookkeeping? previous) =>
        document with
        {
            SeenInRun = seenInRun,
            MediaMissing = previous?.MediaMissing,
            ThumbnailArt = previous?.ThumbnailArt,
        };

    private static TermsQuery InChannels(IReadOnlyCollection<string> channelIds) => new()
    {
        Field = Infer.Field<VideoDocument>(v => v.ChannelId),
        Terms = new TermsQueryField([.. channelIds.Select(FieldValue.String)]),
    };

    private async Task<IReadOnlyList<VideoSummary>> SummariesAsync(Query query, CancellationToken cancellationToken)
    {
        IReadOnlyList<VideoSummaryDocument> hits = await SearchAllAsync<VideoSummaryDocument>(
            query,
            SummaryFields,
            cancellationToken)
            .ConfigureAwait(false);
        return hits
            .Select(h => new VideoSummary(
                h.Id,
                h.Title,
                h.ChannelId,
                DocumentMapper.ParseType(h.Type),
                h.Downloaded,
                h.SeenInRun,
                h.ThumbnailPath))
            .ToArray();
    }
}

internal sealed partial class ChannelRepository(ElasticsearchClient client, IndexNames names)
    : DocumentRepository<Channel, ChannelDocument>(client, names, StoreIndex.Channels), IChannelRepository
{
    private static readonly Fields SummaryFields = Infer.Fields<ChannelSummaryDocument>(
        c => c.Id,
        c => c.Name,
        c => c.SeenInRun!,
        c => c.ThumbPath!,
        c => c.BannerPath!,
        c => c.TvartPath!);

    public async Task<IReadOnlyList<ChannelSummary>> FindNotSeenAsync(string runId, CancellationToken cancellationToken)
    {
        BoolQuery query = new()
        {
            MustNot = [new TermQuery { Field = Infer.Field<ChannelDocument>(c => c.SeenInRun!), Value = runId }],
        };
        IReadOnlyList<ChannelSummaryDocument> hits = await SearchAllAsync<ChannelSummaryDocument>(
            query,
            SummaryFields,
            cancellationToken)
            .ConfigureAwait(false);
        return hits
            .Select(h => new ChannelSummary(
                h.Id,
                h.Name,
                h.SeenInRun,
                new[] { h.ThumbPath, h.BannerPath, h.TvartPath }
                    .OfType<string>()
                    .ToArray()))
            .ToArray();
    }

    protected override ChannelDocument ToDocument(Channel item) => DocumentMapper.ToDocument(item);

    protected override Channel ToDomain(ChannelDocument document) => DocumentMapper.ToChannel(document);

    public Task SetArtAsync(
        ArtSubject subject,
        IReadOnlyDictionary<string, ArtVersions> art,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(art);
        return subject switch
        {
            ArtSubject.ChannelThumb => PartialUpdateEachAsync(
                art.Select(a => KeyValuePair.Create(a.Key, new ThumbArtPartial(DocumentMapper.ToDocument(a.Value)))),
                cancellationToken),
            ArtSubject.ChannelBanner => PartialUpdateEachAsync(
                art.Select(a => KeyValuePair.Create(a.Key, new BannerArtPartial(DocumentMapper.ToDocument(a.Value)))),
                cancellationToken),
            _ => throw new ArgumentOutOfRangeException(nameof(subject), subject, "Only channel thumbs and banners."),
        };
    }

    protected override ChannelDocument WithBookkeeping(
        ChannelDocument document,
        string? seenInRun,
        StoreBookkeeping? previous) =>
        document with { SeenInRun = seenInRun, ThumbArt = previous?.ThumbArt, BannerArt = previous?.BannerArt };
}
