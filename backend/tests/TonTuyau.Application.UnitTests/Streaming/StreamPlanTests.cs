using TonTuyau.Application.Streaming;

namespace TonTuyau.Application.UnitTests.Streaming;

/// <summary>A video's parts (C3): keyframe to keyframe, about 6 s each, and the HLS playlist listing them.</summary>
public sealed class StreamPlanTests
{
    [Fact]
    public void The_parts_start_on_keyframes_at_least_6_s_apart_the_last_one_to_the_end()
    {
        double[] keyframes = [0, 2, 4, 6, 8, 10, 12, 14, 16];

        IReadOnlyList<StreamPart> parts = StreamPlan.Parts(keyframes, 17.5);

        Assert.Equal(
            [new StreamPart(0, 0, 6), new StreamPart(1, 6, 6), new StreamPart(2, 12, 5.5)],
            parts);
    }

    [Fact]
    public void Keyframes_further_apart_make_longer_parts()
    {
        IReadOnlyList<StreamPart> parts = StreamPlan.Parts([0, 10, 25], 30);

        Assert.Equal([(0.0, 10.0), (10.0, 15.0), (25.0, 5.0)], parts.Select(p => (p.Start, p.Duration)));
    }

    [Fact]
    public void Keyframes_past_the_end_are_left_out()
    {
        IReadOnlyList<StreamPart> parts = StreamPlan.Parts([0, 6, 12, 18], 12);

        Assert.Equal([0.0, 6.0], parts.Select(p => p.Start));
        Assert.Equal(12, parts[^1].End);
    }

    [Fact]
    public void Nothing_to_cut_without_keyframes_or_length()
    {
        Assert.Empty(StreamPlan.Parts([], 60));
        Assert.Empty(StreamPlan.Parts([0, 6], 0));
    }

    [Fact]
    public void The_playlist_lists_each_part_after_the_init_segment_whole_and_known_in_advance()
    {
        IReadOnlyList<StreamPart> parts = StreamPlan.Parts([0, 6.006, 12.012], 15.5);

        string playlist = StreamPlan.Playlist(parts, "init.mp4", index => $"{index}.m4s");
        string expected = """
            #EXTM3U
            #EXT-X-VERSION:7
            #EXT-X-PLAYLIST-TYPE:VOD
            #EXT-X-INDEPENDENT-SEGMENTS
            #EXT-X-TARGETDURATION:7
            #EXT-X-MEDIA-SEQUENCE:0
            #EXT-X-MAP:URI="init.mp4"
            #EXTINF:6.006,
            0.m4s
            #EXTINF:6.006,
            1.m4s
            #EXTINF:3.488,
            2.m4s
            #EXT-X-ENDLIST

            """;

        Assert.Equal(expected.ReplaceLineEndings("\n"), playlist);
    }
}
