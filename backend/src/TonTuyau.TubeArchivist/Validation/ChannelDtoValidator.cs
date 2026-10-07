using System.Text.RegularExpressions;
using TonTuyau.Application.Validation;
using TonTuyau.TubeArchivist.Dtos;

namespace TonTuyau.TubeArchivist.Validation;

/// <summary>Rejects a TubeArchivist channel document that TonTuyau can't safely index.</summary>
internal sealed class ChannelDtoValidator : IValidator<ChannelDto>
{
    public void Validate(ChannelDto value)
    {
        ArgumentNullException.ThrowIfNull(value);
        ValidationErrors errors = new();

        bool idOk = value.ChannelId is not null && TaFormats.ChannelId().IsMatch(value.ChannelId);
        if (!idOk)
        {
            errors.Add("channel_id", "must be a 24-character YouTube channel id (UC…).");
        }

        if (string.IsNullOrWhiteSpace(value.ChannelName))
        {
            errors.Add("channel_name", "is required.");
        }

        if (value.ChannelActive is null)
        {
            errors.Add("channel_active", "is required.");
        }

        if (value.ChannelSubs is < 0)
        {
            errors.Add("channel_subs", "must be zero or more when present.");
        }

        if (value.ChannelLastRefresh is not null && !TaFormats.TryParseDate(value.ChannelLastRefresh, out _))
        {
            errors.Add("channel_last_refresh", "must be an ISO-8601 date when present.");
        }

        CheckArt(errors, "channel_thumb_url", value.ChannelThumbUrl, "thumb", idOk ? value.ChannelId : null);
        CheckArt(errors, "channel_banner_url", value.ChannelBannerUrl, "banner", idOk ? value.ChannelId : null);
        CheckArt(errors, "channel_tvart_url", value.ChannelTvartUrl, "tvart", idOk ? value.ChannelId : null);

        errors.ThrowIfAny($"TubeArchivist channel {(idOk ? value.ChannelId : "(no valid id)")}");
    }

    private static void CheckArt(ValidationErrors errors, string field, string? url, string kind, string? channelId)
    {
        if (string.IsNullOrEmpty(url))
        {
            return; // No art is fine.
        }

        Match match = TaFormats.ChannelArtUrl().Match(url);
        if (!match.Success || match.Groups["kind"].Value != kind)
        {
            errors.Add(field, $"must be /cache/channels/<channel_id>_{kind}.<ext> when present.");
        }
        else if (channelId is not null && match.Groups["channel"].Value != channelId)
        {
            errors.Add(field, "must point to this channel's own id.");
        }
    }
}
