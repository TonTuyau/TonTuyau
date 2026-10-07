using System.Text.RegularExpressions;

namespace TonTuyau.IntegrationTests.Support;

/// <summary>
/// Scenarios name videos and channels by short keys ("creme", "tomcooks"); these are their real-looking ids.
/// In paths and expected texts, <c>{video:creme}</c> and <c>{channel:tomcooks}</c> stand for those ids.
/// </summary>
public static partial class TestIds
{
    public static string Video(string key) => FakeTubeArchivist.VideoId(key);

    public static string Channel(string key) => FakeTubeArchivist.ChannelId(key);

    public static string Expand(string text) => Placeholder().Replace(
        text,
        m => m.Groups["kind"].Value == "video" ? Video(m.Groups["key"].Value) : Channel(m.Groups["key"].Value));

    [GeneratedRegex("\\{(?<kind>video|channel):(?<key>[A-Za-z0-9_-]+)\\}")]
    private static partial Regex Placeholder();
}
