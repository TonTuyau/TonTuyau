using System.Collections.Concurrent;
using System.Globalization;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace TonTuyau.IntegrationTests.Support;

/// <summary>
/// A programmable TubeArchivist v0.5.12, over real HTTP, with the quirks measured on the real one:
/// 1-based pages (0 = 1), past the end → 200 + empty data, past the result window → 500 HTML,
/// capped total_hits, /aggs/ totals, 404 JSON for unknown items, 403 without a token.
/// </summary>
public sealed class FakeTubeArchivist : IAsyncDisposable
{
    public static readonly DateTimeOffset Epoch = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private readonly WebApplication _app;
    private readonly object _gate = new();

    private FakeTubeArchivist(WebApplication app) => _app = app;

    public string Url { get; private set; } = "";

    public List<FakeChannel> Channels { get; } = [];

    public List<FakeVideo> Videos { get; } = [];

    public int PageSize { get; set; } = 10;

    public int Window { get; set; } = 10_000;

    /// <summary>When set, every request answers this status (TA down / broken proxy).</summary>
    public int? DownWithStatus { get; set; }

    /// <summary>Requests whose path+query contains the key fail with the status, a given number of times.</summary>
    public ConcurrentDictionary<string, (int Status, int Remaining)> Failures { get; } = new();

    public HashSet<string> MissingArt { get; } = new(StringComparer.Ordinal);

    /// <summary>Images of another size than <see cref="TestImages.DefaultSize"/> (cache path → width, height).</summary>
    public ConcurrentDictionary<string, (int Width, int Height)> ArtSizes { get; } = new(StringComparer.Ordinal);

    /// <summary>Cache files TubeArchivist serves that aren't images at all (the resize must fail, the run go on).</summary>
    public HashSet<string> BrokenArt { get; } = new(StringComparer.Ordinal);

    public ConcurrentQueue<string> Requests { get; } = new();

    /// <summary>Runs (once) before answering the first request whose path+query contains the key.</summary>
    public ConcurrentDictionary<string, Action> BeforeAnswer { get; } = new();

    public static async Task<FakeTubeArchivist> StartAsync()
    {
        WebApplicationBuilder builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Logging.ClearProviders();
        WebApplication app = builder.Build();
        FakeTubeArchivist fake = new(app);
        app.Run(fake.HandleAsync);
        await app.StartAsync();
        fake.Url = app.Services
            .GetRequiredService<IServer>()
            .Features
            .Get<IServerAddressesFeature>()!
            .Addresses
            .Single();
        return fake;
    }

    public static string VideoId(string name) => (name + new string('_', 11))[..11];

    public static string ChannelId(string name) => "UC" + (name + new string('_', 22))[..22];

    public FakeChannel Channel(string name) => Channels.Single(c => c.Name == name);

    public FakeVideo Video(string name) => Videos.Single(v => v.Id == VideoId(name));

    public void Mutate(Action change)
    {
        lock (_gate)
        {
            change();
        }
    }

    public int CountRequests(string pathStart) =>
        Requests.Count(r => r.StartsWith(pathStart, StringComparison.Ordinal));

    public ValueTask DisposeAsync() => _app.DisposeAsync();

    private async Task HandleAsync(HttpContext http)
    {
        string path = http.Request.Path.Value ?? "/";
        Requests.Enqueue(path + http.Request.QueryString);
        (int status, string? body, string? contentType) = Answer(http.Request, path);
        http.Response.StatusCode = status;
        http.Response.ContentType = contentType;
        if (ReferenceEquals(body, ArtMarker))
        {
            await http.Response.Body.WriteAsync(ArtBytes(path));
            return;
        }

        await http.Response.WriteAsync(body);
    }

    /// <summary>Stands for "the image's bytes" in <see cref="Answer"/>, whose bodies are text.</summary>
    private static readonly string ArtMarker = new('\u0001', 1);

    private byte[] ArtBytes(string path)
    {
        if (BrokenArt.Contains(path))
        {
            return "<html>not an image</html>"u8.ToArray();
        }

        (int width, int height) = ArtSizes.TryGetValue(path, out (int Width, int Height) size)
            ? size
            : TestImages.DefaultSize(path);
        return TestImages.Jpeg(width, height);
    }

    private (int Status, string Body, string ContentType) Answer(HttpRequest request, string path)
    {
        const string Json = "application/json";
        if (request.Method != "GET" && request.Method != "HEAD")
        {
            return (405, """{"detail":"read-only fake"}""", Json);
        }

        if (!request.Headers.Authorization.ToString().StartsWith("Token ", StringComparison.Ordinal))
        {
            return (403, """{"detail":"Authentication credentials were not provided."}""", Json);
        }

        // Read once: the steps change it while the backend's requests are being answered on other threads.
        if (DownWithStatus is { } down)
        {
            return (down, "<h1>down</h1>", "text/html");
        }

        string key = path + request.QueryString;
        foreach ((string? pattern, Action? action) in BeforeAnswer)
        {
            if (key.Contains(pattern, StringComparison.Ordinal) && BeforeAnswer.TryRemove(pattern, out _))
            {
                Mutate(action);
            }
        }

        foreach ((string? pattern, (int status, int remaining)) in Failures)
        {
            if (key.Contains(pattern, StringComparison.Ordinal) && remaining > 0)
            {
                Failures[pattern] = (status, remaining - 1);
                return (status, "<h1>Server Error</h1>", "text/html");
            }
        }

        lock (_gate)
        {
            return path switch
            {
                "/api/ping/" => (200, """{"response":"pong","user":1,"version":"v0.5.12","ta_update":null}""", Json),
                "/api/stats/video/" => (200, VideoStats().ToJsonString(), Json),
                "/api/stats/channel/" => (200, new JsonObject { ["doc_count"] = Channels.Count }.ToJsonString(), Json),
                "/api/channel/" => (200, ChannelList(Page(request)).ToJsonString(), Json),
                "/api/video/" => VideoList(request),
                _ when path.StartsWith("/api/channel/", StringComparison.Ordinal) => ChannelItem(path),
                _ when path.StartsWith("/api/video/", StringComparison.Ordinal) => VideoItem(path),
                _ when path.StartsWith("/cache/", StringComparison.Ordinal) => MissingArt.Contains(path)
                    ? (404, "<h1>Not Found</h1>", "text/html")
                    : (200, ArtMarker, "image/jpeg"),
                _ => (404, """{"detail":"Not found."}""", Json),
            };
        }
    }

    private static int Page(HttpRequest request)
    {
        int page = int.Parse(request.Query["page"].FirstOrDefault() ?? "0", CultureInfo.InvariantCulture);
        return Math.Max(page, 1);
    }

    private JsonObject VideoStats()
    {
        JsonObject Count(int n) => new() { ["doc_count"] = n };
        return new JsonObject
        {
            ["doc_count"] = Videos.Count,
            ["type_videos"] = Count(Videos.Count(v => v.Type == "videos")),
            ["type_shorts"] = Count(Videos.Count(v => v.Type == "shorts")),
            ["type_streams"] = Count(Videos.Count(v => v.Type == "streams")),
            ["active_true"] = Count(Videos.Count(v => v.Active)),
            ["active_false"] = Videos.Any(v => !v.Active) ? Count(Videos.Count(v => !v.Active)) : null,
        };
    }

    private JsonObject ChannelList(int page)
    {
        FakeChannel[] all = Channels
            .OrderBy(c => c.Name, StringComparer.Ordinal)
            .ToArray();
        IEnumerable<JsonObject> items = all
            .Skip((page - 1) * PageSize)
            .Take(PageSize)
            .Select(ChannelJson);
        return Paged(items, all.Length, page);
    }

    private (int, string, string) VideoList(HttpRequest request)
    {
        int page = Page(request);
        if ((page - 1) * PageSize >= Window)
        {
            return (500, "<!doctype html><title>Server Error (500)</title>", "text/html");
        }

        IEnumerable<FakeVideo> list = Videos.Where(v => !v.HiddenFromLists);
        if (request.Query["channel"].FirstOrDefault() is { } channel)
        {
            list = list.Where(v => v.ChannelId == channel);
        }

        if (request.Query["type"].FirstOrDefault() is { } type)
        {
            list = list.Where(v => v.Type == type);
        }

        bool desc = request.Query["order"].FirstOrDefault() == "desc";
        Func<FakeVideo, long> key = request.Query["sort"].FirstOrDefault() == "downloaded"
            ? v => v.Downloaded
            : v => v.Published.ToUnixTimeSeconds();
        FakeVideo[] sorted = (desc ? list.OrderByDescending(key) : list.OrderBy(key))
            .ThenBy(v => v.Id, StringComparer.Ordinal)
            .ToArray();
        // DroppedFromPages: counted in total_hits but never returned, like a TA list answering with missing data.
        IEnumerable<JsonObject> items = sorted
            .Skip((page - 1) * PageSize)
            .Take(PageSize)
            .Where(v => !v.DroppedFromPages)
            .Select(VideoJson);
        return (200, Paged(items, sorted.Length, page).ToJsonString(), "application/json");
    }

    private (int, string, string) ChannelItem(string path)
    {
        string[] parts = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        FakeChannel? channel = Channels.SingleOrDefault(c => c.Id == parts[2]);
        if (parts.Length == 4 && parts[3] == "aggs")
        {
            // Like TA v0.5.12: an unknown channel answers 0, not 404.
            int total = Videos.Count(v => v.ChannelId == parts[2]);
            JsonObject aggs = new() { ["total_items"] = new JsonObject { ["value"] = total } };
            return (200, aggs.ToJsonString(), "application/json");
        }

        return channel is null
            ? (404, """{"error":"channel not found"}""", "application/json")
            : (200, ChannelJson(channel).ToJsonString(), "application/json");
    }

    private (int, string, string) VideoItem(string path)
    {
        string id = path.Split('/', StringSplitOptions.RemoveEmptyEntries)[2];
        FakeVideo? video = Videos.SingleOrDefault(v => v.Id == id);
        return video is null
            ? (404, """{"error":"video not found"}""", "application/json")
            : (200, VideoJson(video).ToJsonString(), "application/json");
    }

    private JsonObject Paged(IEnumerable<JsonObject> items, int total, int page) => new()
    {
        ["data"] = new JsonArray(items.ToArray<JsonNode>()),
        ["paginate"] = new JsonObject
        {
            ["page_size"] = PageSize,
            ["page_from"] = (page - 1) * PageSize,
            ["current_page"] = page,
            ["max_hits"] = total >= Window,
            ["total_hits"] = Math.Min(total, Window),
            ["last_page"] = 0,
        },
    };

    private static JsonObject ChannelJson(FakeChannel c) => new()
    {
        ["channel_id"] = c.Id,
        ["channel_name"] = c.Name,
        ["channel_description"] = $"Description of {c.Name}",
        ["channel_active"] = true,
        ["channel_subs"] = 1000,
        ["channel_last_refresh"] = "2026-09-01T00:00:00+00:00",
        ["channel_tags"] = new JsonArray("fake"),
        ["channel_tabs"] = new JsonArray("videos", "shorts", "streams"),
        ["channel_thumb_url"] = $"/cache/channels/{c.Id}_thumb.jpg",
        ["channel_banner_url"] = $"/cache/channels/{c.Id}_banner.jpg",
        ["channel_tvart_url"] = $"/cache/channels/{c.Id}_tvart.jpg",
    };

    private JsonObject VideoJson(FakeVideo v)
    {
        FakeChannel channel = Channels.SingleOrDefault(c => c.Id == v.ChannelId)
            ?? new FakeChannel(v.ChannelId, "gone");
        return new JsonObject
        {
            ["youtube_id"] = v.Id,
            ["title"] = v.Broken ? null : v.Title,
            ["description"] = "",
            ["vid_type"] = v.Type,
            ["active"] = v.Active,
            ["published"] = v.Published.ToString("yyyy-MM-ddTHH:mm:sszzz", CultureInfo.InvariantCulture),
            ["date_downloaded"] = v.Downloaded,
            ["vid_last_refresh"] = "2026-09-01T00:00:00+00:00",
            ["media_size"] = 1000,
            ["media_url"] = $"/youtube/{v.MediaFolder ?? v.ChannelId}/{v.Id}.mp4",
            ["vid_thumb_url"] = $"/cache/videos/{char.ToLowerInvariant(v.Id[0])}/{v.Id}.jpg",
            ["tags"] = new JsonArray(),
            ["category"] = new JsonArray("Test"),
            ["playlist"] = new JsonArray(),
            ["player"] = new JsonObject { ["watched"] = false, ["duration"] = 60 },
            ["stats"] = new JsonObject
            {
                ["view_count"] = v.Views,
                ["like_count"] = 1,
                ["dislike_count"] = 0,
                ["average_rating"] = 0.0,
            },
            ["streams"] = new JsonArray(
                new JsonObject
                {
                    ["type"] = "video",
                    ["codec"] = "h264",
                    ["bitrate"] = 1000,
                    ["width"] = 1920,
                    ["height"] = 1080,
                    ["index"] = 0,
                },
                new JsonObject { ["type"] = "audio", ["codec"] = "opus", ["bitrate"] = 128, ["index"] = 1 }),
            ["channel"] = ChannelJson(channel),
        };
    }
}

public sealed record FakeChannel(string Id, string Name);

public sealed class FakeVideo(string id, string channelId, long downloaded)
{
    public string Id { get; } = id;

    public string ChannelId { get; set; } = channelId;

    public long Downloaded { get; set; } = downloaded;

    public DateTimeOffset Published { get; set; } = DateTimeOffset.FromUnixTimeSeconds(downloaded).AddDays(-1);

    public string Title { get; set; } = $"Video {id.TrimEnd('_')}";

    public string Type { get; set; } = "videos";

    public bool Active { get; set; } = true;

    public long Views { get; set; } = 10;

    /// <summary>Sent with a missing title: fails TonTuyau's validation, but keeps a valid id.</summary>
    public bool Broken { get; set; }

    /// <summary>Exists (detail, stats, aggs) but missing from list pages: what a buggy or truncated TA list looks like.</summary>
    public bool HiddenFromLists { get; set; }

    /// <summary>Counted in the list's total_hits but missing from its pages.</summary>
    public bool DroppedFromPages { get; set; }

    /// <summary>Another channel's folder in media_url (R4-2).</summary>
    public string? MediaFolder { get; set; }
}
