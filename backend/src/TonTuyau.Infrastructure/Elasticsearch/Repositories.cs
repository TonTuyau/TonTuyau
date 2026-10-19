using Elastic.Clients.Elasticsearch;
using Elastic.Clients.Elasticsearch.QueryDsl;
using TonTuyau.Application.Library;
using TonTuyau.Application.Store;
using TonTuyau.Infrastructure.Elasticsearch.Documents;

namespace TonTuyau.Infrastructure.Elasticsearch;

/// <summary>
/// The index's copy of the library's videos (2.0): written from PostgreSQL (<c>LibraryVideoRepository</c>), read by
/// the lists, the search and the counts.
/// </summary>
internal sealed partial class VideoIndex(ElasticsearchClient client, IndexNames names)
    : DocumentRepository<StoredVideo, VideoDocument>(client, names, StoreIndex.Videos)
{
    /// <summary>Sets or clears the "file missing on the share" flag of the documents there.</summary>
    public Task SetMediaMissingAsync(IReadOnlyCollection<string> ids, bool missing, CancellationToken cancellationToken)
        =>
        PartialUpdateAsync(ids, new MediaMissingPartial(missing), cancellationToken);

    /// <summary>Records the thumbnails' versions in the documents there.</summary>
    public Task SetThumbnailArtAsync(IReadOnlyDictionary<string, ArtVersions> art, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(art);
        return PartialUpdateEachAsync(
            art.Select(a => KeyValuePair.Create(a.Key, new ThumbnailArtPartial(DocumentMapper.ToDocument(a.Value)))),
            cancellationToken);
    }

    protected override VideoDocument ToDocument(StoredVideo item) => DocumentMapper.ToDocument(item);

    private static TermsQuery InChannels(IReadOnlyCollection<string> channelIds) => new()
    {
        Field = Infer.Field<VideoDocument>(v => v.ChannelId),
        Terms = new TermsQueryField([.. channelIds.Select(FieldValue.String)]),
    };
}

/// <summary>The index's copy of the library's channels (2.0). Same rules as <see cref="VideoIndex"/>.</summary>
internal sealed partial class ChannelIndex(ElasticsearchClient client, IndexNames names)
    : DocumentRepository<StoredChannel, ChannelDocument>(client, names, StoreIndex.Channels)
{
    /// <summary>Records one kind of the channels' pictures' versions (thumb or banner) in the documents there.</summary>
    /// <exception cref="ArgumentOutOfRangeException">Not a channel thumb or banner.</exception>
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

    protected override ChannelDocument ToDocument(StoredChannel item) => DocumentMapper.ToDocument(item);
}
