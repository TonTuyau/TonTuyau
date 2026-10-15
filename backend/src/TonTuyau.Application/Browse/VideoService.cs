using TonTuyau.Application.Library;
using TonTuyau.Application.Media;
using TonTuyau.Application.Paging;
using TonTuyau.Application.Store;
using TonTuyau.Application.Validation;
using TonTuyau.Application.YouTube;

namespace TonTuyau.Application.Browse;

/// <summary>Home page, the video lists and the watch page.</summary>
public sealed class VideoService(
    IVideoRepository videos,
    IYouTubeFactsSource facts,
    CardBuilder cards,
    IValidator<HomeRequest> homeValidator,
    IValidator<VideoListRequest> listValidator)
{
    /// <summary>Sections in this order; an empty one is left out.</summary>
    public static readonly IReadOnlyList<VideoType> HomeSections = [VideoType.Video, VideoType.Short, VideoType.Stream];

    /// <summary>The latest published of each type (R2-19).</summary>
    public async Task<IReadOnlyList<HomeSectionView>> HomeAsync(
        HomeRequest request,
        CancellationToken cancellationToken)
    {
        homeValidator.Validate(request);
        PageRequest page = new(request.Size ?? ApiValues.DefaultPageSize, null);
        StorePage<VideoListItem>[] found = await Task
            .WhenAll(HomeSections.Select(type => videos.FindAsync(
                new VideoQuery(null, new VideoFilter(Type: type), VideoListSort.Latest),
                page,
                cancellationToken)))
            .ConfigureAwait(false);

        // One lookup of the channels for every section.
        IReadOnlyList<VideoCardView> all = await cards
            .VideoCardsAsync(
                [.. found.SelectMany(f => f.Items)],
                cancellationToken)
            .ConfigureAwait(false);
        List<HomeSectionView> sections = [];
        int offset = 0;
        for (int i = 0; i < HomeSections.Count; i++)
        {
            int count = found[i].Items.Count;
            if (count > 0)
            {
                sections.Add(new HomeSectionView(
                    HomeSections[i],
                    [.. all
                        .Skip(offset)
                        .Take(count)]));
            }

            offset += count;
        }

        return sections;
    }

    public async Task<ResultPage<VideoCardView>> ListAsync(
        VideoListRequest request,
        CancellationToken cancellationToken)
    {
        listValidator.Validate(request);
        VideoQuery query = new(
            null,
            new VideoFilter(Type: TypeOf(request.Type), ExcludeId: request.Exclude),
            SortOf(request.Sort));
        return await ListAsync("videos", query, request.Cursor, request.Size, cancellationToken).ConfigureAwait(false);
    }

    /// <exception cref="NotFoundException">No video with this id.</exception>
    public async Task<VideoDetailView> GetAsync(string id, CancellationToken cancellationToken)
    {
        RequestRules.RequireVideoId(id);
        StoredVideo stored = await videos.GetStoredAsync(id, cancellationToken).ConfigureAwait(false)
            ?? throw new NotFoundException($"There's no video with the id '{id}'.");
        Video video = stored.Video;
        IReadOnlyList<VideoCardView> card = await cards
            .VideoCardsAsync([CardBuilder.ListItemOf(video, stored.ThumbnailArt)], cancellationToken)
            .ConfigureAwait(false);
        return new VideoDetailView(
            card[0],
            video.Description,
            video.Downloaded,
            video.Stats.Likes,
            video.Tags,
            video.Categories,
            video.MediaSize,
            MediaTypes.PlaybackType(MediaTypes.ExtensionOf(video.MediaPath), video.Tracks),
            video.Tracks,
            !stored.MediaMissing,
            await ChaptersAsync(video.Id, cancellationToken).ConfigureAwait(false));
    }

    /// <summary>Its chapters as TonTuyau read them on YouTube (1.5, PostgreSQL); none until it refreshed the video.</summary>
    private async Task<IReadOnlyList<YouTubeChapter>> ChaptersAsync(string id, CancellationToken cancellationToken) =>
        (await facts.GetVideosAsync([id], cancellationToken).ConfigureAwait(false))
            .GetValueOrDefault(id)
            ?.Refreshed
            ?.Chapters ?? [];

    /// <summary>A page of a plain (not searched) video list: the shared part of /api/videos and a channel's tabs.</summary>
    internal async Task<ResultPage<VideoCardView>> ListAsync(
        string scope,
        VideoQuery query,
        string? cursor,
        int? size,
        CancellationToken cancellationToken)
    {
        string fingerprint = Fingerprints.Of(scope, query);
        PageRequest page = new(
            size ?? ApiValues.DefaultPageSize,
            CursorCodec.Decode(cursor, fingerprint)?.After);
        StorePage<VideoListItem> found = await videos.FindAsync(query, page, cancellationToken).ConfigureAwait(false);
        return await cards.VideoPageAsync(found, fingerprint, cancellationToken).ConfigureAwait(false);
    }

    internal static VideoType? TypeOf(string? type) => type is null ? null : ApiValues.Types[type];

    internal static VideoListSort SortOf(string? sort) =>
        sort is null ? VideoListSort.Latest : ApiValues.VideoSorts[sort];
}
