using System.Text.RegularExpressions;
using TonTuyau.Application.Validation;
using TonTuyau.TubeArchivist.Dtos;

namespace TonTuyau.TubeArchivist.Validation;

/// <summary>Rejects a TubeArchivist video document that TonTuyau can't safely index or serve.</summary>
internal sealed class VideoDtoValidator : IValidator<VideoDto>
{
    public void Validate(VideoDto value)
    {
        ArgumentNullException.ThrowIfNull(value);
        ValidationErrors errors = new();

        bool idOk = value.YoutubeId is not null && TaFormats.VideoId().IsMatch(value.YoutubeId);
        if (!idOk)
        {
            errors.Add("youtube_id", "must be an 11-character YouTube id.");
        }

        string? channelId = value.Channel?.ChannelId;
        bool channelOk = channelId is not null && TaFormats.ChannelId().IsMatch(channelId);
        if (!channelOk)
        {
            errors.Add("channel.channel_id", "must be a 24-character YouTube channel id (UC…).");
        }

        if (string.IsNullOrWhiteSpace(value.Channel?.ChannelName))
        {
            errors.Add("channel.channel_name", "is required.");
        }

        if (string.IsNullOrWhiteSpace(value.Title))
        {
            errors.Add("title", "is required.");
        }

        if (value.VidType is null || !TaFormats.VideoTypes.ContainsKey(value.VidType))
        {
            errors.Add("vid_type", "must be one of videos, shorts, streams.");
        }

        if (value.Active is null)
        {
            errors.Add("active", "is required.");
        }

        if (!TaFormats.TryParseDate(value.Published, out _))
        {
            errors.Add("published", "must be an ISO-8601 date.");
        }

        if (value.DateDownloaded is not > 0)
        {
            errors.Add("date_downloaded", "must be a positive epoch timestamp.");
        }

        if (value.VidLastRefresh is not null && !TaFormats.TryParseDate(value.VidLastRefresh, out _))
        {
            errors.Add("vid_last_refresh", "must be an ISO-8601 date when present.");
        }

        if (value.MediaSize is not >= 0)
        {
            errors.Add("media_size", "must be zero or more.");
        }

        if (value.Player?.Duration is not >= 0)
        {
            errors.Add("player.duration", "must be zero or more seconds.");
        }

        Match? media = value.MediaUrl is null ? null : TaFormats.MediaUrl().Match(value.MediaUrl);
        if (media is not { Success: true })
        {
            errors.Add("media_url", "must be /youtube/<channel_id>/<video_id>.<ext>.");
        }
        else if (idOk && media.Groups["video"].Value != value.YoutubeId)
        {
            // Another channel's folder is tolerated (R4-2: the mapper corrects it); another video's file is not.
            errors.Add("media_url", "must point to this video's own id.");
        }

        Match? thumb = value.VidThumbUrl is null ? null : TaFormats.VideoThumbUrl().Match(value.VidThumbUrl);
        if (thumb is not { Success: true })
        {
            errors.Add("vid_thumb_url", "must be /cache/videos/<c>/<video_id>.<ext>.");
        }
        else if (idOk && thumb.Groups["video"].Value != value.YoutubeId)
        {
            errors.Add("vid_thumb_url", "must point to this video's own id.");
        }

        if (value.Streams is null)
        {
            errors.Add("streams", "is required (may be empty).");
        }
        else
        {
            for (int i = 0; i < value.Streams.Count; i++)
            {
                if (value.Streams[i] is { Type: "video" or "audio" } s && string.IsNullOrWhiteSpace(s.Codec))
                {
                    errors.Add($"streams[{i}].codec", "is required.");
                }
            }
        }

        errors.ThrowIfAny($"TubeArchivist video {(idOk ? value.YoutubeId : "(no valid id)")}");
    }
}
