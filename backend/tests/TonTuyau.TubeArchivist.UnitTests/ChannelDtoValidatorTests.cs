using System.Text.Json;
using TonTuyau.Application.Validation;
using TonTuyau.TubeArchivist.Dtos;
using TonTuyau.TubeArchivist.UnitTests.Support;
using TonTuyau.TubeArchivist.Validation;

namespace TonTuyau.TubeArchivist.UnitTests;

public sealed class ChannelDtoValidatorTests
{
    private readonly ChannelDtoValidator _validator = new();

    public enum Broken
    {
        IdMissing,
        IdBad,
        NameBlank,
        ActiveMissing,
        SubsNegative,
        RefreshBad,
        ThumbTraversal,
        ThumbWrongKind,
        BannerOtherChannel,
        TvArtElsewhere,
    }

    [Fact]
    public void Every_recorded_channel_is_valid()
    {
        JsonElement[] all = Data("channels-page-1")
            .Concat(Data("channels-page-2"))
            .Append(FixtureHandler.Get("channel-small").Json)
            .ToArray();

        foreach (JsonElement item in all)
        {
            _validator.Validate(item.Deserialize<ChannelDto>(TaJson.Options)!);
        }

        Assert.True(all.Length > 100);
    }

    [Theory, CombinatorialData]
    public void Each_broken_field_is_reported_by_name(Broken broken)
    {
        ChannelDto c = ValidChannel();
        string id = c.ChannelId!;
        (ChannelDto? dto, string? field) = broken switch
        {
            Broken.IdMissing => (c with { ChannelId = null }, "channel_id"),
            Broken.IdBad => (c with { ChannelId = "XX" + id[2..] }, "channel_id"),
            Broken.NameBlank => (c with { ChannelName = "" }, "channel_name"),
            Broken.ActiveMissing => (c with { ChannelActive = null }, "channel_active"),
            Broken.SubsNegative => (c with { ChannelSubs = -5 }, "channel_subs"),
            Broken.RefreshBad => (c with { ChannelLastRefresh = "never" }, "channel_last_refresh"),
            Broken.ThumbTraversal => (
                c with
                {
                    ChannelThumbUrl
                    = $"/cache/channels/../../{id}_thumb.jpg"
                },
                "channel_thumb_url"),
            Broken.ThumbWrongKind => (
                c with
                {
                    ChannelThumbUrl
                    = $"/cache/channels/{id}_banner.jpg"
                },
                "channel_thumb_url"),
            Broken.BannerOtherChannel => (
                c with
                {
                    ChannelBannerUrl
                    = "/cache/channels/UCzzzzzzzzzzzzzzzzzzzzzz_banner.jpg"
                },
                "channel_banner_url"),
            Broken.TvArtElsewhere => (
                c with
                {
                    ChannelTvartUrl
                    = "https://elsewhere.example/tvart.jpg"
                },
                "channel_tvart_url"),
            _ => throw new ArgumentOutOfRangeException(nameof(broken)),
        };

        ValidationException ex = Assert.Throws<ValidationException>(() => _validator.Validate(dto));

        Assert.Contains(ex.Errors, e => e.Field == field);
    }

    [Theory, CombinatorialData]
    public void Missing_art_is_fine([CombinatorialValues(null, "")] string? art)
    {
        _validator.Validate(
            ValidChannel() with { ChannelThumbUrl = art, ChannelBannerUrl = art, ChannelTvartUrl = art });
    }

    private static JsonElement.ArrayEnumerator Data(string fixture) =>
        FixtureHandler.Get(fixture).Json.GetProperty("data").EnumerateArray();

    private static ChannelDto ValidChannel() =>
        FixtureHandler.Get("channel-small").Json.Deserialize<ChannelDto>(TaJson.Options)!;
}
