using System.Globalization;
using TonTuyau.Application.Browse;
using TonTuyau.Application.Library;
using TonTuyau.Application.Paging;
using TonTuyau.Application.Store;

namespace TonTuyau.Api.Contracts;

/// <summary>Every URL the front uses, built here and nowhere else (the front never builds one).</summary>
public static class ApiUrls
{
    public static string VideoThumbnail(string id) => $"/art/videos/{Uri.EscapeDataString(id)}";

    public static string VideoThumbnail(string id, int width) =>
        $"{VideoThumbnail(id)}/{width.ToString(CultureInfo.InvariantCulture)}";

    public static string ChannelArt(string id, ChannelArtKind kind) =>
        $"/art/channels/{Uri.EscapeDataString(id)}/{ApiValues.ArtKinds.NameOf(kind)}";

    public static string ChannelArt(string id, ChannelArtKind kind, int width) =>
        $"{ChannelArt(id, kind)}/{width.ToString(CultureInfo.InvariantCulture)}";

    public static string Media(string id) => $"/media/{Uri.EscapeDataString(id)}";

    public static string Download(string id) => $"/media/{Uri.EscapeDataString(id)}/download";
}

/// <summary>Service answers → contract shapes.</summary>
public static class ApiMapper
{
    public static VideoCard ToDto(VideoCardView card)
    {
        ArgumentNullException.ThrowIfNull(card);
        return new VideoCard
        {
            Id = card.Id,
            Title = card.Title,
            Type = card.Type,
            Published = card.Published.UtcDateTime,
            DurationSeconds = Seconds(card.Duration),
            Views = card.Views,
            Active = card.Active,
            ThumbnailUrl = card.Thumbnail is null ? null : ApiUrls.VideoThumbnail(card.Id),
            Thumbnails = VideoThumbnails(card),
            Channel = Channel(card.Channel),
        };
    }

    public static VideoDetail ToDto(VideoDetailView detail)
    {
        ArgumentNullException.ThrowIfNull(detail);
        VideoCardView card = detail.Card;
        return new VideoDetail
        {
            Id = card.Id,
            Title = card.Title,
            Type = card.Type,
            Published = card.Published.UtcDateTime,
            DurationSeconds = Seconds(card.Duration),
            Views = card.Views,
            Active = card.Active,
            ThumbnailUrl = card.Thumbnail is null ? null : ApiUrls.VideoThumbnail(card.Id),
            Thumbnails = VideoThumbnails(card),
            Channel = new VideoDetailChannel(
                card.Channel.Id,
                card.Channel.Name,
                ChannelThumbnail(card.Channel.Id, card.Channel.Thumbnail),
                ChannelThumbnails(card.Channel.Id, card.Channel.Thumbnail),
                card.Channel.Subscribers),
            Description = detail.Description,
            Downloaded = detail.Downloaded.UtcDateTime,
            Likes = detail.Likes,
            Tags = detail.Tags,
            Categories = detail.Categories,
            FileSize = detail.FileSize,
            MimeType = detail.MimeType,
            Tracks = [.. detail.Tracks.Select(Track)],
            MediaUrl = ApiUrls.Media(card.Id),
            DownloadUrl = ApiUrls.Download(card.Id),
            MediaAvailable = detail.MediaAvailable,
            Chapters = [.. detail.Chapters.Select(c => new Chapter(c.StartSeconds, c.EndSeconds, c.Title))],
        };
    }

    public static ChannelCard ToDto(ChannelCardView card)
    {
        ArgumentNullException.ThrowIfNull(card);
        return new ChannelCard
        {
            Id = card.Id,
            Name = card.Name,
            Subscribers = card.Subscribers,
            ThumbnailUrl = ChannelThumbnail(card.Id, card.Thumbnail),
            Thumbnails = ChannelThumbnails(card.Id, card.Thumbnail),
            VideoCounts = Counts(card.VideoCounts),
        };
    }

    public static ChannelDetail ToDto(ChannelDetailView detail)
    {
        ArgumentNullException.ThrowIfNull(detail);
        ChannelCardView card = detail.Card;
        return new ChannelDetail
        {
            Id = card.Id,
            Name = card.Name,
            Subscribers = card.Subscribers,
            ThumbnailUrl = ChannelThumbnail(card.Id, card.Thumbnail),
            Thumbnails = ChannelThumbnails(card.Id, card.Thumbnail),
            VideoCounts = Counts(card.VideoCounts),
            Description = detail.Description,
            BannerUrl = detail.Banner is null ? null : ApiUrls.ChannelArt(card.Id, ChannelArtKind.Banner),
            Banners = Sizes(
                detail.Banner,
                ApiUrls.ChannelArt(card.Id, ChannelArtKind.Banner),
                w => ApiUrls.ChannelArt(card.Id, ChannelArtKind.Banner, w)),
            Tags = detail.Tags,
            Active = detail.Active,
        };
    }

    public static Page<VideoCard> ToDto(ResultPage<VideoCardView> page)
    {
        ArgumentNullException.ThrowIfNull(page);
        return new Page<VideoCard>([.. page.Items.Select(ToDto)], page.NextCursor, page.Total);
    }

    public static Page<ChannelCard> ToDto(ResultPage<ChannelCardView> page)
    {
        ArgumentNullException.ThrowIfNull(page);
        return new Page<ChannelCard>([.. page.Items.Select(ToDto)], page.NextCursor, page.Total);
    }

    public static HomeResponse ToDto(IReadOnlyList<HomeSectionView> sections)
    {
        ArgumentNullException.ThrowIfNull(sections);
        return new HomeResponse(
        [
            .. sections.Select(s => new HomeSection(
                s.Type,
                [.. s.Items.Select(ToDto)])),
        ]);
    }

    /// <summary>The three shapes of <c>/api/search</c>, by kind.</summary>
    public static object ToDto(SearchResultsView results)
    {
        ArgumentNullException.ThrowIfNull(results);
        return results.Kind switch
        {
            SearchKind.Channels => ToDto(results.Channels!),
            SearchKind.Videos => ToDto(results.Videos!),
            // Later pages have no channels: an empty list, never an absent field (contract v1.1).
            _ => new SearchAllResponse(
                [.. (results.TopChannels ?? []).Select(ToDto)],
                ToDto(results.Videos!)),
        };
    }

    public static SuggestResponse ToDto(IReadOnlyList<SuggestionView> suggestions)
    {
        ArgumentNullException.ThrowIfNull(suggestions);
        return new SuggestResponse([.. suggestions.Select(s => new Suggestion(s.Text, s.Kind, s.Id))]);
    }

    /// <summary>
    /// An image's versions as the contract lists them: the resized ones by width, then the original with its real
    /// width. No image: an empty list.
    /// </summary>
    public static IReadOnlyList<ImageSize> Sizes(ArtVersions? art, string originalUrl, Func<int, string> sizedUrl)
    {
        ArgumentNullException.ThrowIfNull(sizedUrl);
        if (art is null)
        {
            return [];
        }

        return
        [
            .. art.Widths
                .Where(w => w < art.OriginalWidth)
                .Order()
                .Select(w => new ImageSize(w, sizedUrl(w))),
            new ImageSize(art.OriginalWidth, originalUrl),
        ];
    }

    private static IReadOnlyList<ImageSize> VideoThumbnails(VideoCardView card) =>
        Sizes(card.Thumbnail, ApiUrls.VideoThumbnail(card.Id), w => ApiUrls.VideoThumbnail(card.Id, w));

    private static VideoChannel Channel(ChannelRefView channel) =>
        new(
            channel.Id,
            channel.Name,
            ChannelThumbnail(channel.Id, channel.Thumbnail),
            ChannelThumbnails(channel.Id, channel.Thumbnail));

    private static string? ChannelThumbnail(string id, ArtVersions? thumbnail) =>
        thumbnail is null ? null : ApiUrls.ChannelArt(id, ChannelArtKind.Thumb);

    private static IReadOnlyList<ImageSize> ChannelThumbnails(string id, ArtVersions? thumbnail) =>
        Sizes(
            thumbnail,
            ApiUrls.ChannelArt(id, ChannelArtKind.Thumb),
            w => ApiUrls.ChannelArt(id, ChannelArtKind.Thumb, w));

    private static Track Track(MediaTrack track) =>
        new(track.Kind, track.Codec, track.Width, track.Height, track.Bitrate);

    private static VideoCounts Counts(TypeCounts counts) => new(counts.Videos, counts.Shorts, counts.Streams);

    private static long Seconds(TimeSpan duration) => (long)Math.Round(duration.TotalSeconds);
}
