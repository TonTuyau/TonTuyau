using TonTuyau.Application.YouTube;

namespace TonTuyau.YouTube.UnitTests;

/// <summary>A tab's listing (2.0, B5): a line of <c>--print "%(.{id,live_status})j"</c> per entry.</summary>
public sealed class YouTubeListingTests
{
    [Theory]
    [InlineData("""{"id": "dQw4w9WgXcQ", "live_status": "not_live"}""", false)]
    [InlineData("""{"id": "dQw4w9WgXcQ", "live_status": "was_live"}""", false)]
    [InlineData("""{"id": "dQw4w9WgXcQ", "live_status": null}""", false)]
    [InlineData("""{"id": "dQw4w9WgXcQ"}""", false)]
    [InlineData("""{"id": "dQw4w9WgXcQ", "live_status": "is_upcoming"}""", true)]
    [InlineData("""{"id": "dQw4w9WgXcQ", "live_status": "is_live"}""", true)]
    public void An_entry_is_its_id_and_whether_it_is_still_to_come(string line, bool notYet) =>
        Assert.Equal(new YouTubeListed("dQw4w9WgXcQ", notYet), YouTubeClient.ListedOf(line));

    [Theory]
    [InlineData("")]
    [InlineData("WARNING: [youtube:tab] Incomplete data received")]
    [InlineData("""{"id": "UCmw5b3sYz6v1sFkwM1HLs1g", "live_status": null}""")]
    [InlineData("""{"id": null}""")]
    [InlineData("""{"id": "dQw4w9WgXcQ", """)]
    public void Anything_else_is_no_entry(string line) => Assert.Null(YouTubeClient.ListedOf(line));
}
