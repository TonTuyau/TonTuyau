using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace TonTuyau.FixtureRecorder.UnitTests;

public sealed partial class AnonymiserTests
{
    private const string RealVideo = "QF4xxljhWIA";
    private const string RealChannel = "UCo0U1tbk3YbqiLDhkeWOviQ";
    private const string RealPlaylist = "PLabcdefghijklmnopqrstuvwxyz012345";

    [GeneratedRegex("^[A-Za-z0-9_-]{11}\\z")]
    private static partial Regex VideoId();

    [GeneratedRegex("^UC[A-Za-z0-9_-]{22}\\z")]
    private static partial Regex ChannelId();

    private const string RealJson = $$"""
        {
          "youtube_id": "{{RealVideo}}",
          "title": "Real title with a real name: Bob Lennon",
          "description": "Real description, call me at bob@example.org",
          "tags": ["bob", "lennon", "fanta", "a", "b", "c", "d", "e", "f", "g"],
          "playlist": ["{{RealPlaylist}}"],
          "media_url": "/youtube/{{RealChannel}}/{{RealVideo}}.mp4",
          "vid_thumb_url": "/cache/videos/q/{{RealVideo}}.jpg",
          "media_size": 162685,
          "active": false,
          "comment_count": null,
          "subtitles": [],
          "player": { "duration": 3 },
          "channel": {
            "channel_id": "{{RealChannel}}",
            "channel_name": "Bob Lennon",
            "channel_description": "",
            "channel_thumb_url": "/cache/channels/{{RealChannel}}_thumb.jpg"
          }
        }
        """;

    private static Fixture Video(string request = $"/api/video/{RealVideo}/") => new(
        "video",
        request,
        true,
        200,
        "application/json",
        JsonNode.Parse(RealJson),
        null);

    [Fact]
    public void No_real_id_name_or_text_survives()
    {
        Fixture result = new Anonymiser("salt").Anonymise([Video()]).Single();
        string all = result.Request + result.Json!.ToJsonString();

        string[] reals = [RealVideo, RealChannel, RealPlaylist, "Bob", "Lennon", "bob@example.org", "fanta"];
        foreach (string real in reals)
        {
            Assert.DoesNotContain(real, all, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void Fake_ids_keep_the_real_formats()
    {
        Anonymiser anonymiser = new("salt");

        JsonNode json = anonymiser.Anonymise([Video()]).Single().Json!;

        Assert.Matches(VideoId(), json["youtube_id"]!.GetValue<string>());
        Assert.Matches(ChannelId(), json["channel"]!["channel_id"]!.GetValue<string>());
        Assert.Equal(RealPlaylist.Length, json["playlist"]![0]!.GetValue<string>().Length);
    }

    [Fact]
    public void Ids_are_replaced_consistently_everywhere_including_paths_and_requests()
    {
        Anonymiser anonymiser = new("salt");

        IReadOnlyList<Fixture> results = anonymiser.Anonymise(
            [Video(), Video($"/api/video/?channel={RealChannel}&page=1")]);

        string video = anonymiser.IdMap[RealVideo];
        string channel = anonymiser.IdMap[RealChannel];
        JsonNode json = results[0].Json!;
        Assert.Equal($"/api/video/{video}/", results[0].Request);
        Assert.Equal($"/api/video/?channel={channel}&page=1", results[1].Request);
        Assert.Equal($"/youtube/{channel}/{video}.mp4", json["media_url"]!.GetValue<string>());
        Assert.Equal($"/cache/channels/{channel}_thumb.jpg", json["channel"]!["channel_thumb_url"]!.GetValue<string>());
    }

    [Fact]
    public void Thumbnail_folder_follows_the_fake_id()
    {
        Anonymiser anonymiser = new("salt");

        JsonNode json = anonymiser.Anonymise([Video()]).Single().Json!;

        string fake = anonymiser.IdMap[RealVideo];
        Assert.Equal(
            $"/cache/videos/{char.ToLowerInvariant(fake[0])}/{fake}.jpg",
            json["vid_thumb_url"]!.GetValue<string>());
    }

    [Fact]
    public void Structure_numbers_nulls_and_empty_values_are_kept()
    {
        JsonNode json = new Anonymiser("salt").Anonymise([Video()]).Single().Json!;

        Assert.Equal(162685, json["media_size"]!.GetValue<int>());
        Assert.False(json["active"]!.GetValue<bool>());
        Assert.Null(json["comment_count"]);
        Assert.True(json.AsObject().ContainsKey("comment_count"));
        Assert.Empty(json["subtitles"]!.AsArray());
        Assert.Equal("", json["channel"]!["channel_description"]!.GetValue<string>());
        Assert.Equal(3, json["player"]!["duration"]!.GetValue<int>());
    }

    [Fact]
    public void Texts_are_capped_and_tags_trimmed()
    {
        string longText = new('x', 5000);
        string raw = $$"""{"description": "{{longText}}", "tags": ["a","b","c","d","e","f","g","h","i","j"]}""";
        Fixture fixture = Video() with { Json = JsonNode.Parse(raw) };

        JsonNode json = new Anonymiser("salt").Anonymise([fixture]).Single().Json!;

        Assert.True(json["description"]!.GetValue<string>().Length <= Anonymiser.MaxDescriptionLength);
        Assert.Equal(Anonymiser.MaxTags, json["tags"]!.AsArray().Count);
    }

    [Theory, CombinatorialData]
    public void Same_salt_gives_same_output_and_another_salt_does_not(bool sameSalt)
    {
        string a = new Anonymiser("salt-a").Anonymise([Video()]).Single().Json!.ToJsonString();
        string b = new Anonymiser(sameSalt ? "salt-a" : "salt-b").Anonymise([Video()]).Single().Json!.ToJsonString();

        Assert.Equal(sameSalt, a == b);
    }

    [Fact]
    public void The_input_is_not_modified()
    {
        Fixture input = Video();
        string before = input.Json!.ToJsonString();

        new Anonymiser("salt").Anonymise([input]);

        Assert.Equal(before, input.Json!.ToJsonString());
    }

    [Fact]
    public void Text_bodies_get_ids_replaced()
    {
        Fixture html = Video() with { Json = null, Text = $"<p>{RealVideo}</p>" };
        Anonymiser anonymiser = new("salt");

        IReadOnlyList<Fixture> result = anonymiser.Anonymise([Video(), html]);

        Assert.Equal($"<p>{anonymiser.IdMap[RealVideo]}</p>", result[1].Text);
    }
}
