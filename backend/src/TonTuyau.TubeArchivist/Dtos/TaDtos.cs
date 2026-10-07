using System.Text.Json;
using System.Text.Json.Serialization;

namespace TonTuyau.TubeArchivist.Dtos;

// Raw TubeArchivist v0.5.12 JSON shapes. Everything is nullable: the validators decide what is required.

internal static class TaJson
{
    public static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        NumberHandling = JsonNumberHandling.AllowReadingFromString,
    };
}

internal sealed record PagedDto(List<JsonElement>? Data, PaginateDto? Paginate);

internal sealed record PaginateDto(int? PageSize, int? PageFrom, int? CurrentPage, bool? MaxHits, long? TotalHits);

internal sealed record PingDto(string? Response, string? Version);

internal sealed record CountDto(long? DocCount);

internal sealed record ChannelStatsDto(long? DocCount);

internal sealed record AggValueDto(long? Value);

internal sealed record ChannelAggsDto(AggValueDto? TotalItems);

internal sealed record VideoStatsDto(
    long? DocCount,
    CountDto? TypeVideos,
    CountDto? TypeShorts,
    CountDto? TypeStreams,
    CountDto? ActiveTrue,
    CountDto? ActiveFalse);

internal sealed record VideoDto(
    string? YoutubeId,
    string? Title,
    string? Description,
    string? VidType,
    bool? Active,
    string? Published,
    long? DateDownloaded,
    string? VidLastRefresh,
    long? MediaSize,
    string? MediaUrl,
    string? VidThumbUrl,
    List<string?>? Tags,
    List<string?>? Category,
    List<string?>? Playlist,
    PlayerDto? Player,
    StatsDto? Stats,
    List<StreamDto?>? Streams,
    ChannelDto? Channel);

internal sealed record PlayerDto(double? Duration);

internal sealed record StatsDto(long? ViewCount, long? LikeCount, long? DislikeCount);

internal sealed record StreamDto(string? Type, string? Codec, long? Bitrate, int? Width, int? Height);

internal sealed record ChannelDto(
    string? ChannelId,
    string? ChannelName,
    string? ChannelDescription,
    bool? ChannelActive,
    long? ChannelSubs,
    string? ChannelLastRefresh,
    List<string?>? ChannelTags,
    List<string?>? ChannelTabs,
    string? ChannelThumbUrl,
    string? ChannelBannerUrl,
    string? ChannelTvartUrl);
