using System.Text.Json;
using System.Text.Json.Nodes;
using TonTuyau.Application.Library;
using TonTuyau.Application.TubeArchivist;
using TonTuyau.FixtureRecorder;
using TonTuyau.TubeArchivist;
using TonTuyau.TubeArchivist.Http;

// Records TubeArchivist answers for the unit tests, read-only, and anonymises them.
//   TONTUYAU_TA_URL=… TONTUYAU_TA_TOKEN=… dotnet run --project tools/TonTuyau.FixtureRecorder -- <output dir> [<salt>]
// Or `-- scan`: walk the whole library through the client and report what the validators reject (console only).
// Every request goes through ReadOnlyGuardHandler, like in the app.

if (args.Length < 1)
{
    Console.Error
        .WriteLine("usage: TonTuyau.FixtureRecorder <output dir> [<salt>]   |   TonTuyau.FixtureRecorder scan");
    return 2;
}

string baseUrl = Environment.GetEnvironmentVariable("TONTUYAU_TA_URL")
    ?? throw new InvalidOperationException("TONTUYAU_TA_URL is not set.");
string token = Environment.GetEnvironmentVariable("TONTUYAU_TA_TOKEN")
    ?? throw new InvalidOperationException("TONTUYAU_TA_TOKEN is not set.");
string output = args[0];
string salt = args.Length > 1 ? args[1] : "tontuyau-fixtures";

const string SmallChannel = "UCTD7VWPkRliCUEIdYb7GQGA"; // 3 videos: a channel whose list ends on page 1.
const string InactiveVideo = "QF4xxljhWIA";
const string Av1FourKVideo = "y07hZ0XvwUg";
const string MissingVideo = "aaaaaaaaaaa";
const string MissingChannel = "UCaaaaaaaaaaaaaaaaaaaaaa";

string Videos(VideoSort sort, SortOrder order, int page, string? channel = null, VideoType? type = null) =>
    TubeArchivistGateway.BuildVideoListUrl(
        new VideoPageQuery(sort, order, channel, type),
        page);

(string Name, string Path, bool WithToken)[] requests = new (string Name, string Path, bool WithToken)[]
{
    ("ping", "api/ping/", true),
    ("ping-no-token", "api/ping/", false),
    ("stats-video", "api/stats/video/", true),
    ("stats-channel", "api/stats/channel/", true),
    ("channels-page-0", "api/channel/?page=0", true),
    ("channels-page-1", "api/channel/?page=1", true),
    ("channels-page-2", "api/channel/?page=2", true),
    ("channels-page-3", "api/channel/?page=3", true),
    ("videos-downloaded-desc-page-1", Videos(VideoSort.Downloaded, SortOrder.Desc, 1), true),
    ("videos-downloaded-desc-page-2", Videos(VideoSort.Downloaded, SortOrder.Desc, 2), true),
    ("videos-downloaded-desc-page-100", Videos(VideoSort.Downloaded, SortOrder.Desc, 100), true),
    ("videos-downloaded-desc-page-101", Videos(VideoSort.Downloaded, SortOrder.Desc, 101), true),
    ("videos-small-channel-page-1", Videos(VideoSort.Published, SortOrder.Desc, 1, SmallChannel), true),
    ("videos-small-channel-page-2", Videos(VideoSort.Published, SortOrder.Desc, 2, SmallChannel), true),
    ("videos-unknown-channel-page-1", Videos(VideoSort.Published, SortOrder.Desc, 1, MissingChannel), true),
    ("videos-shorts-page-1", Videos(VideoSort.Published, SortOrder.Desc, 1, type: VideoType.Short), true),
    ("videos-streams-page-1", Videos(VideoSort.Published, SortOrder.Desc, 1, type: VideoType.Stream), true),
    ("videos-sort-filesize", "api/video/?sort=filesize&order=desc&page=1", true),
    ("video-inactive", $"api/video/{InactiveVideo}/", true),
    ("video-av1-4k", $"api/video/{Av1FourKVideo}/", true),
    ("video-missing", $"api/video/{MissingVideo}/", true),
    ("channel-small", $"api/channel/{SmallChannel}/", true),
    ("channel-missing", $"api/channel/{MissingChannel}/", true),
    ("channel-small-aggs", $"api/channel/{SmallChannel}/aggs/", true),
    ("channel-missing-aggs", $"api/channel/{MissingChannel}/aggs/", true),
};

HttpClient Client(bool withToken)
{
    HttpMessageHandler handler = new HttpClientHandler();
    if (withToken)
    {
        handler = new TokenAuthHandler(token) { InnerHandler = handler };
    }

    return new HttpClient(new ReadOnlyGuardHandler(handler)) { BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/") };
}

using HttpClient withToken = Client(true);
using HttpClient withoutToken = Client(false);

if (args[0] == "scan")
{
    return await LibraryScan.RunAsync(withToken);
}

List<Fixture> recorded = new();
foreach ((string? name, string? path, bool auth) in requests)
{
    using HttpResponseMessage response = await (auth ? withToken : withoutToken).GetAsync(
        new Uri(path, UriKind.Relative));
    string? contentType = response.Content.Headers.ContentType?.MediaType;
    string body = await response.Content.ReadAsStringAsync();
    JsonNode? json = null;
    if (contentType == "application/json")
    {
        json = JsonNode.Parse(body);
    }

    recorded.Add(
        new Fixture(name, "/" + path, auth, (int)response.StatusCode, contentType, json, json is null ? body : null));
    Console.WriteLine($"{(int)response.StatusCode} {name}");
}

IReadOnlyList<Fixture> anonymised = new Anonymiser(salt).Anonymise(recorded);

// Last line of defence: nothing real may be written.
string all = string.Join('\n', anonymised.Select(f => f.Request + (f.Json?.ToJsonString() ?? f.Text)));
if (all.Contains(token, StringComparison.Ordinal) || all.Contains(SmallChannel, StringComparison.Ordinal)
    || all.Contains(InactiveVideo, StringComparison.Ordinal))
{
    Console.Error.WriteLine("Anonymisation check failed: a token or real id is still present. Nothing written.");
    return 1;
}

Directory.CreateDirectory(output);
JsonSerializerOptions writeOptions = new()
{
    WriteIndented = true,
    Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
};
foreach (Fixture f in anonymised)
{
    JsonObject envelope = new()
    {
        ["request"] = f.Request,
        ["authenticated"] = f.Authenticated,
        ["status"] = f.Status,
        ["contentType"] = f.ContentType,
        ["json"] = f.Json,
        ["text"] = f.Text,
    };
    await File.WriteAllTextAsync(
        Path.Combine(output, f.Name + ".json"),
        envelope.ToJsonString(writeOptions) + "\n");
}

Console.WriteLine($"{anonymised.Count} fixtures written to {output}");
return 0;
