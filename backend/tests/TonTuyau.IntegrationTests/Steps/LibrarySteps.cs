using System.Globalization;
using Microsoft.Extensions.DependencyInjection;
using TonTuyau.Application.Configuration;
using TonTuyau.Application.Library;
using TonTuyau.Application.Store;
using TonTuyau.Application.Sync;
using TonTuyau.Application.YouTube;
using TonTuyau.IntegrationTests.Support;

namespace TonTuyau.IntegrationTests.Steps;

/// <summary>
/// The library the API scenarios browse: written straight through the repositories (as the sync would), with the
/// media share and the art store in temp folders.
/// </summary>
[Binding]
public sealed class LibrarySteps(ScenarioState state) : IDisposable
{
    /// <summary>Default publication date of a seeded video when the table doesn't give one.</summary>
    public static readonly DateTimeOffset DefaultPublished = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

    private readonly string _root = state.Temporary(Path.Combine(
        Path.GetTempPath(),
        "tontuyau-api-" + Guid.NewGuid().ToString("N")[..8]));
    private readonly Dictionary<string, Video> _videos = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Channel> _channels = new(StringComparer.Ordinal);

    public string MediaRoot => Path.Combine(_root, "media");

    public string ArtRoot => Path.Combine(_root, "art");

    private IServiceProvider Services => state.Backend.Services;

    private IArtResizer Resizer => Services.GetRequiredService<IArtResizer>();

    [BeforeScenario("api")]
    public void UseTempFolders()
    {
        Directory.CreateDirectory(MediaRoot);
        Directory.CreateDirectory(ArtRoot);
        Set(ConfigKeys.MediaRoot, MediaRoot);
        Set(ConfigKeys.ArtRoot, ArtRoot);
        Set(ConfigKeys.SyncLogRoot, Path.Combine(_root, "logs"));
    }

    /// <summary>
    /// Columns: key, name, subscribers, tags (comma-separated), description, active (TubeArchivist's flag), youtube
    /// (TonTuyau's state: not checked, on YouTube, retry later, gone). Art is copied.
    /// </summary>
    [Given("the channels:")]
    public async Task TheChannels(DataTable table)
    {
        foreach (DataTableRow row in table.Rows)
        {
            string key = row["key"];
            string id = TestIds.Channel(key);
            Channel channel = new(
                id,
                Get(row, "name") ?? key,
                Get(row, "description") ?? "",
                (long)Number(Get(row, "subscribers") ?? "1000"),
                bool.Parse(Get(row, "active") ?? "true"),
                null,
                List(Get(row, "tags")),
                [VideoType.Video, VideoType.Short, VideoType.Stream],
                $"/cache/channels/{id}_thumb.jpg",
                $"/cache/channels/{id}_banner.jpg",
                $"/cache/channels/{id}_tvart.jpg",
                YouTubeOf(Get(row, "youtube")));
            _channels[key] = channel;
            foreach (string art in (string[])[channel.ThumbPath!, channel.BannerPath!, channel.TvArtPath!])
            {
                WriteArt(art);
            }
        }

        await state.WaitUntilReadyAsync();
        await Services
            .GetRequiredService<IChannelRepository>()
            .UpsertAsync([.. _channels.Values], null, default);
        await RefreshAsync();
    }

    /// <summary>
    /// Columns: key, title, channel (a channel key), type (video/short/stream), published (ISO date), views,
    /// duration (seconds), tags, description, active (TubeArchivist's flag), youtube (TonTuyau's state: not checked, on
    /// YouTube, retry later, gone), file (extension), codecs (e.g. <c>av1+opus</c>).
    /// Thumbnails are copied; media files are not (see the "on the media share" step).
    /// </summary>
    [Given("the videos:")]
    public async Task TheVideos(DataTable table)
    {
        List<Video> added = [];
        foreach (DataTableRow row in table.Rows)
        {
            string key = row["key"];
            string id = TestIds.Video(key);
            string channelKey = Get(row, "channel") ?? throw new InvalidOperationException($"{key}: no channel.");
            Channel channel = _channels[channelKey];
            string extension = Get(row, "file") ?? "mp4";
            string[] codecs = (Get(row, "codecs") ?? "av1+opus").Split('+');
            Video video = new(
                id,
                channel.Id,
                channel.Name,
                Get(row, "title") ?? key,
                Get(row, "description") ?? "",
                Type(Get(row, "type") ?? "video"),
                bool.Parse(Get(row, "active") ?? "true"),
                Get(row, "published") is { } published ? Date(published) : DefaultPublished,
                DefaultPublished.AddDays(30),
                null,
                TimeSpan.FromSeconds(Number(Get(row, "duration") ?? "600")),
                1000,
                $"{channel.Id}/{id}.{extension}",
                false,
                $"/cache/videos/{id[0]}/{id}.jpg",
                List(Get(row, "tags")),
                ["Howto & Style"],
                [],
                new VideoStats((long)Number(Get(row, "views") ?? "0"), 7, 0),
                [
                    new MediaTrack(MediaTrackKind.Video, codecs[0], 2_000_000, 1920, 1080),
                    .. codecs
                        .Skip(1)
                        .Select(c => new MediaTrack(MediaTrackKind.Audio, c, 128_000, null, null)),
                ],
                YouTubeOf(Get(row, "youtube")));
            _videos[key] = video;
            added.Add(video);
            WriteArt(video.ThumbnailPath);
        }

        await state.WaitUntilReadyAsync();
        await Services
            .GetRequiredService<IVideoRepository>()
            .UpsertAsync(added, null, default);
        await RefreshAsync();
    }

    [Given("the thumbnail of the video {string} was never copied")]
    public void ThumbnailNeverCopied(string key) => DeleteArt(_videos[key].ThumbnailPath);

    [Given("the {word} of the channel {string} was never copied")]
    public void ChannelArtNeverCopied(string kind, string key)
    {
        Channel channel = _channels[key];
        DeleteArt(kind switch
        {
            "thumbnail" => channel.ThumbPath!,
            "banner" => channel.BannerPath!,
            "tvart" => channel.TvArtPath!,
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "thumbnail, banner or tvart"),
        });
    }

    // ---------------------------------------------------------------- resized art (QA09)

    [Given("the resized versions of the video {string} were made")]
    public void VideoVersionsMade(string key) =>
        Resizer.EnsureVersions(_videos[key].ThumbnailPath, ArtSizes.VideoThumbnail);

    [Given("the resized versions of the channel {string} were made")]
    public void ChannelVersionsMade(string key)
    {
        Resizer.EnsureVersions(_channels[key].ThumbPath!, ArtSizes.ChannelThumb);
        Resizer.EnsureVersions(_channels[key].BannerPath!, ArtSizes.ChannelBanner);
    }

    [Given("the thumbnail of the video {string} is {int} px wide")]
    public void ThumbnailWidth(string key, int width) =>
        File.WriteAllBytes(ArtFileOf(_videos[key].ThumbnailPath), TestImages.Jpeg(width, Math.Max(1, width * 9 / 16)));

    [Given("the sync recorded the thumbnail of the video {string} as {int} px wide with the versions {string}")]
    public async Task RecordedVideo(string key, int width, string widths)
    {
        await Services
            .GetRequiredService<IVideoRepository>()
            .SetThumbnailArtAsync(
                new Dictionary<string, ArtVersions> { [TestIds.Video(key)] = new(width, Widths(widths)) },
                default);
        await RefreshAsync();
    }

    [Given("the sync recorded the {word} of the channel {string} as {int} px wide with the versions {string}")]
    public async Task RecordedChannel(string kind, string key, int width, string widths)
    {
        ArtSubject subject = kind switch
        {
            "thumbnail" => ArtSubject.ChannelThumb,
            "banner" => ArtSubject.ChannelBanner,
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "thumbnail or banner"),
        };
        await Services
            .GetRequiredService<IChannelRepository>()
            .SetArtAsync(
                subject,
                new Dictionary<string, ArtVersions> { [TestIds.Channel(key)] = new(width, Widths(widths)) },
                default);
        await RefreshAsync();
    }

    /// <summary>Byte i of the file is <c>i % 251</c>, so any range of it can be checked.</summary>
    [Given("the file of the video {string} is on the media share with {int} bytes")]
    public async Task FileOnShare(string key, int length)
    {
        string file = Path.Combine(MediaRoot, _videos[key].MediaPath);
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        await File.WriteAllBytesAsync(file, Content(0, length));
    }

    [Given("the full sync flagged the file of the video {string} as missing")]
    public async Task FlaggedMissing(string key)
    {
        await Services
            .GetRequiredService<IVideoRepository>()
            .SetMediaMissingAsync([TestIds.Video(key)], true, default);
        await RefreshAsync();
    }

    /// <summary>What a path traversal would be after: a file beside the media and art roots.</summary>
    [Given("a file {string} sits next to the media share")]
    public void FileNextToTheShare(string name) => File.WriteAllText(Path.Combine(_root, name), "secret");

    public string FileOf(string key) => Path.Combine(MediaRoot, _videos[key].MediaPath);

    public string ArtFileOf(string cachePath) => Path.Combine(ArtRoot, cachePath["/cache/".Length..]);

    public Video VideoOf(string key) => _videos[key];

    public Channel ChannelOf(string key) => _channels[key];

    public static byte[] Content(long start, long count)
    {
        byte[] bytes = new byte[count];
        for (long i = 0; i < count; i++)
        {
            bytes[i] = (byte)((start + i) % 251);
        }

        return bytes;
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private void WriteArt(string cachePath)
    {
        string file = ArtFileOf(cachePath);
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        (int width, int height) = TestImages.DefaultSize(cachePath);
        File.WriteAllBytes(file, TestImages.Jpeg(width, height));
    }

    private void DeleteArt(string cachePath) => File.Delete(ArtFileOf(cachePath));

    private Task RefreshAsync() =>
        Services
            .GetRequiredService<IIndexManager>()
            .RefreshAsync(default);

    private void Set(string key, string value) => state.Settings.Set(ConfigKeys.EnvironmentName(key), value);

    /// <summary>
    /// "gone", "private", "unlisted", "retry later", "on YouTube", "not checked": TonTuyau's state, checked a day ago;
    /// empty: none.
    /// </summary>
    private static YouTubeStatus? YouTubeOf(string? state) => state switch
    {
        null => null,
        "gone" => new YouTubeStatus(YouTubeState.Gone, DefaultPublished.AddDays(40), "Video unavailable", null),
        "private" => new YouTubeStatus(YouTubeState.Private, DefaultPublished.AddDays(40), "Private video", null),
        "unlisted" => new YouTubeStatus(YouTubeState.Unlisted, DefaultPublished.AddDays(40), null, null),
        "retry later" => new YouTubeStatus(YouTubeState.RetryLater, DefaultPublished.AddDays(40), "Sign in", null),
        "on YouTube" => new YouTubeStatus(YouTubeState.OnYouTube, DefaultPublished.AddDays(40), null, null),
        "not checked" => new YouTubeStatus(YouTubeState.NotChecked, null, null, null),
        _ => throw new ArgumentOutOfRangeException(
            nameof(state),
            state,
            "gone, private, unlisted, retry later, on YouTube, not checked"),
    };

    private static string? Get(DataTableRow row, string column) =>
        row.TryGetValue(column, out string? value) && value.Length > 0 ? value : null;

    private static double Number(string text) => double.Parse(text, CultureInfo.InvariantCulture);

    private static int[] Widths(string text) =>
        text
            .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Select(w => int.Parse(w, CultureInfo.InvariantCulture))
            .ToArray();

    private static string[] List(string? text) =>
        text?.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries) ?? [];

    private static DateTimeOffset Date(string text) =>
        DateTimeOffset.Parse(text, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal);

    private static VideoType Type(string text) => text switch
    {
        "video" => VideoType.Video,
        "short" => VideoType.Short,
        "stream" => VideoType.Stream,
        _ => throw new ArgumentOutOfRangeException(nameof(text), text, "video, short or stream"),
    };
}
