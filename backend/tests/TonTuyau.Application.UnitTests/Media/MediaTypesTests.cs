using TonTuyau.Application.Library;
using TonTuyau.Application.Media;

namespace TonTuyau.Application.UnitTests.Media;

public sealed class MediaTypesTests
{
    [Theory, CombinatorialData]
    public void The_playback_type_names_the_container_and_one_string_per_codec(
        [CombinatorialValues("mp4", "m4v", "mov", "webm", "mkv")] string extension,
        [CombinatorialValues("av1", "vp9", "vp8", "h264", "hevc", "h265")] string video,
        [CombinatorialValues(null, "opus", "aac", "mp3", "vorbis", "flac")] string? audio)
    {
        List<MediaTrack> tracks = [new MediaTrack(MediaTrackKind.Video, video, 1, 1920, 1080)];
        if (audio is not null)
        {
            tracks.Add(new MediaTrack(MediaTrackKind.Audio, audio, 1, null, null));
        }

        string container = extension is "webm" or "mkv" ? "video/webm" : "video/mp4";
        string codecs = audio is null ? Expected[video] : $"{Expected[video]}, {Expected[audio]}";
        Assert.Equal(
            $"{container}; codecs=\"{codecs}\"",
            MediaTypes.PlaybackType(extension, tracks));
    }

    [Fact]
    public void Video_codecs_come_first_and_each_codec_once()
    {
        MediaTrack[] tracks =
        [
            new(MediaTrackKind.Audio, "opus", 1, null, null),
            new(MediaTrackKind.Video, "av1", 1, 3840, 2160),
            new(MediaTrackKind.Audio, "opus", 1, null, null),
        ];

        Assert.Equal(
            "video/mp4; codecs=\"av01.0.08M.08, opus\"",
            MediaTypes.PlaybackType("mp4", tracks));
    }

    [Fact]
    public void Without_tracks_only_the_container_is_given() =>
        Assert.Equal(
            "video/mp4",
            MediaTypes.PlaybackType("mp4", []));

    [Theory]
    [InlineData("AV1", "av01.0.08M.08")]
    [InlineData("prores", "prores")]
    [InlineData("pcm_s16le", "pcm_s16le")]
    [InlineData("x\"; codecs=\"evil", "xcodecsevil")]
    [InlineData("\"\"", "unknown")]
    public void An_unknown_codec_keeps_its_name_made_safe_so_the_browser_says_no(string codec, string expected) =>
        Assert.Equal(expected, MediaTypes.CodecString(codec));

    [Theory]
    [InlineData("mp4", "video/mp4")]
    [InlineData("m4v", "video/mp4")]
    [InlineData("mov", "video/quicktime")]
    [InlineData("webm", "video/webm")]
    [InlineData("mkv", "video/x-matroska")]
    [InlineData("avi", "application/octet-stream")]
    public void Files_are_served_as_what_they_are(string extension, string type) =>
        Assert.Equal(type, MediaTypes.FileContentType(extension));

    [Theory]
    [InlineData("jpg", "image/jpeg")]
    [InlineData("jpeg", "image/jpeg")]
    [InlineData("png", "image/png")]
    [InlineData("webp", "image/webp")]
    public void Images_are_served_as_what_they_are(string extension, string type) =>
        Assert.Equal(type, MediaTypes.ImageContentType(extension));

    [Theory]
    [InlineData("UCaaaaaaaaaaaaaaaaaaaaaa/abcdefghijk.MP4", "mp4")]
    [InlineData("/cache/videos/a/abcdefghijk.webp", "webp")]
    [InlineData("noextension", "")]
    public void Extensions_are_read_in_lower_case(string path, string extension) =>
        Assert.Equal(extension, MediaTypes.ExtensionOf(path));

    /// <summary>The table of the milestone notes, written out again on purpose.</summary>
    private static readonly Dictionary<string, string> Expected = new()
    {
        ["av1"] = "av01.0.08M.08",
        ["vp9"] = "vp09.00.40.08",
        ["vp8"] = "vp8",
        ["h264"] = "avc1.640028",
        ["hevc"] = "hvc1.1.6.L120.90",
        ["h265"] = "hvc1.1.6.L120.90",
        ["opus"] = "opus",
        ["aac"] = "mp4a.40.2",
        ["mp3"] = "mp4a.6B",
        ["vorbis"] = "vorbis",
        ["flac"] = "flac",
    };
}
