using System.Text.RegularExpressions;
using TonTuyau.Application.Library;
using TonTuyau.Application.YouTube;
using TonTuyau.TubeArchivist.Dtos;
using TonTuyau.TubeArchivist.Validation;

namespace TonTuyau.TubeArchivist.Mapping;

/// <summary>DTO → domain. Only called on DTOs that passed their validator, hence the null-forgiving operators.</summary>
internal static class TaMapper
{
    public static Video ToVideo(VideoDto dto)
    {
        DateTimeOffset published = TaFormats.TryParseDate(dto.Published, out DateTimeOffset p)
            ? p
            : throw new InvalidOperationException("Unvalidated video DTO.");
        Match media = TaFormats.MediaUrl().Match(dto.MediaUrl!);
        string channelId = dto.Channel!.ChannelId!;

        return new Video(
            Id: dto.YoutubeId!,
            ChannelId: channelId,
            ChannelName: dto.Channel.ChannelName ?? "",
            Title: dto.Title!,
            Description: dto.Description ?? "",
            Type: TaFormats.VideoTypes[dto.VidType!],
            Published: published,
            Downloaded: DateTimeOffset.FromUnixTimeSeconds(dto.DateDownloaded!.Value),
            Duration: TimeSpan.FromSeconds(dto.Player!.Duration!.Value),
            MediaSize: dto.MediaSize!.Value,
            // R4-2: TA sometimes keeps a stale folder in media_url; the file lives under the video's own channel.
            MediaPath: $"{channelId}/{dto.YoutubeId}.{media.Groups["ext"].Value}",
            MediaPathCorrected: media.Groups["channel"].Value != channelId,
            ThumbnailPath: dto.VidThumbUrl!,
            Tags: Clean(dto.Tags),
            Categories: Clean(dto.Category),
            Stats: new VideoStats(dto.Stats?.ViewCount ?? 0, dto.Stats?.LikeCount ?? 0, dto.Stats?.DislikeCount ?? 0),
            Tracks: dto.Streams!
                .Where(s => s is { Type: "video" or "audio" })
                .Select(s => new MediaTrack(
                    s!.Type == "video" ? MediaTrackKind.Video : MediaTrackKind.Audio,
                    s.Codec!,
                    s.Bitrate ?? 0,
                    s.Width,
                    s.Height))
                .ToArray(),
            // What TonTuyau knows of it on YouTube: nothing yet (TubeArchivist's own flag isn't kept, 2.0).
            YouTube: YouTubeStatus.NotChecked);
    }

    public static Channel ToChannel(ChannelDto dto) =>
        new(
            Id: dto.ChannelId!,
            Name: dto.ChannelName!,
            Description: dto.ChannelDescription ?? "",
            Subscribers: dto.ChannelSubs ?? 0,
            Tags: Clean(dto.ChannelTags),
            ThumbPath: NullIfEmpty(dto.ChannelThumbUrl),
            BannerPath: NullIfEmpty(dto.ChannelBannerUrl),
            TvArtPath: NullIfEmpty(dto.ChannelTvartUrl),
            YouTube: YouTubeStatus.NotChecked);

    private static string[] Clean(List<string?>? values) =>
        values is null
            ? []
            : values
                .Where(v => !string.IsNullOrWhiteSpace(v))
                .Select(v => v!)
                .ToArray();

    private static string? NullIfEmpty(string? value) => string.IsNullOrEmpty(value) ? null : value;
}
