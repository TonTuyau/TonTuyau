using TonTuyau.Application.Library;

namespace TonTuyau.Application.UnitTests.Library;

public sealed class LibraryIdsTests
{
    [Theory]
    [InlineData("dQw4w9WgXcQ")]
    [InlineData("  dQw4w9WgXcQ  ")]
    [InlineData("https://www.youtube.com/watch?v=dQw4w9WgXcQ")]
    [InlineData("https://www.youtube.com/watch?feature=share&v=dQw4w9WgXcQ&t=42s")]
    [InlineData("http://youtube.com/watch?v=dQw4w9WgXcQ")]
    [InlineData("youtube.com/watch?v=dQw4w9WgXcQ")]
    [InlineData("https://m.youtube.com/watch?v=dQw4w9WgXcQ")]
    [InlineData("https://music.youtube.com/watch?v=dQw4w9WgXcQ&list=RDdQw4w9WgXcQ")]
    [InlineData("https://youtu.be/dQw4w9WgXcQ?si=abc")]
    [InlineData("youtu.be/dQw4w9WgXcQ")]
    [InlineData("https://www.youtube.com/shorts/dQw4w9WgXcQ")]
    [InlineData("https://www.youtube.com/live/dQw4w9WgXcQ?feature=share")]
    [InlineData("https://www.youtube.com/embed/dQw4w9WgXcQ")]
    public void A_video_id_is_read_from_the_id_or_a_YouTube_link(string text) =>
        Assert.Equal("dQw4w9WgXcQ", LibraryIds.VideoIdOf(text));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("dQw4w9WgXc")]
    [InlineData("https://www.youtube.com/watch?v=short")]
    [InlineData("https://www.youtube.com/watch")]
    [InlineData("https://www.youtube.com/@SomeChannel")]
    [InlineData("https://www.youtube.com/channel/UCmw5b3sYz6v1sFkwM1HLs1g")]
    [InlineData("https://vimeo.com/watch?v=dQw4w9WgXcQ")]
    [InlineData("https://evil.example/youtu.be/dQw4w9WgXcQ")]
    [InlineData("ftp://youtu.be/dQw4w9WgXcQ")]
    [InlineData("not a link at all")]
    public void Anything_else_gives_no_id(string? text) => Assert.Null(LibraryIds.VideoIdOf(text));

    [Theory]
    [InlineData("UCmw5b3sYz6v1sFkwM1HLs1g", "UCmw5b3sYz6v1sFkwM1HLs1g")]
    [InlineData("https://www.youtube.com/channel/UCmw5b3sYz6v1sFkwM1HLs1g", "UCmw5b3sYz6v1sFkwM1HLs1g")]
    [InlineData("youtube.com/channel/UCmw5b3sYz6v1sFkwM1HLs1g/videos", "UCmw5b3sYz6v1sFkwM1HLs1g")]
    [InlineData("@UnderTheCity", "@UnderTheCity")]
    [InlineData("  @under.the-city_2  ", "@under.the-city_2")]
    [InlineData("https://www.youtube.com/@UnderTheCity", "@UnderTheCity")]
    [InlineData("https://m.youtube.com/@UnderTheCity/streams?view=0", "@UnderTheCity")]
    public void A_channel_is_read_from_its_id_its_handle_or_a_link_to_either(string text, string expected) =>
        Assert.Equal(expected, LibraryIds.ChannelTargetOf(text));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("@ab")]
    [InlineData("@has space")]
    [InlineData("UCshort")]
    [InlineData("https://www.youtube.com/watch?v=dQw4w9WgXcQ")]
    [InlineData("https://www.youtube.com/c/OldCustomName")]
    [InlineData("https://vimeo.com/@UnderTheCity")]
    [InlineData("https://evil.example/youtube.com/@UnderTheCity")]
    public void Anything_else_gives_no_channel(string? text) => Assert.Null(LibraryIds.ChannelTargetOf(text));

    [Theory]
    [InlineData("videos,shorts,streams", true, true, true)]
    [InlineData("streams, videos", true, false, true)]
    [InlineData("shorts", false, true, false)]
    public void Tabs_are_read_by_their_names(string names, bool videos, bool shorts, bool streams) =>
        Assert.Equal(new ChannelTabs(videos, shorts, streams), ChannelTabs.Parse(names));

    [Theory]
    [InlineData("")]
    [InlineData(",")]
    [InlineData("videos,podcasts")]
    [InlineData("Videos")]
    public void Anything_else_is_no_tabs(string names) => Assert.Null(ChannelTabs.Parse(names));
}
