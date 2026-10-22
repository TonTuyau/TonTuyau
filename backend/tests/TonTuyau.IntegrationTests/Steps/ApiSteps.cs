using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using SkiaSharp;
using TonTuyau.IntegrationTests.Support;

namespace TonTuyau.IntegrationTests.Steps;

/// <summary>
/// Calls the REST API like the front does and checks its answers. Requests and expected texts may use
/// <c>{video:key}</c> / <c>{channel:key}</c> for the ids of seeded items (see <see cref="TestIds"/>).
/// </summary>
[Binding]
public sealed class ApiSteps(ScenarioState state, LibrarySteps library)
{
    private string? _lastPath;

    private JsonNode Json => JsonNode.Parse(Encoding.UTF8.GetString(state.ResponseBody))
        ?? throw new InvalidOperationException("The body is JSON null.");

    // ---------------------------------------------------------------- requests

    [When("I request {string} with the header {string}")]
    public Task RequestWithHeader(string path, string header) => SendAsync(HttpMethod.Get, path, header);

    [When("I request {string} with the headers {string} and {string}")]
    public Task RequestWithHeaders(string path, string first, string second) =>
        SendAsync(HttpMethod.Get, path, first, second);

    [When("I send a {word} request to {string}")]
    public Task OtherMethod(string method, string path) => SendAsync(new HttpMethod(method), path);

    [When("I list {string}")]
    public async Task List(string path)
    {
        _lastPath = path;
        await SendAsync(HttpMethod.Get, path);
        AssertStatus(HttpStatusCode.OK);
    }

    [When("I ask for the next page")]
    public async Task NextPage()
    {
        string cursor = NextCursorOf(Json) ?? throw new InvalidOperationException("There is no next page.");
        await SendAsync(HttpMethod.Get, WithCursor(_lastPath!, cursor));
    }

    [When("I ask for the next page with {string} instead")]
    public async Task NextPageElsewhere(string otherPath)
    {
        string cursor = NextCursorOf(Json) ?? throw new InvalidOperationException("There is no next page.");
        await SendAsync(HttpMethod.Get, WithCursor(otherPath, cursor));
    }

    /// <summary>Follows nextCursor to the end, keeping every title seen.</summary>
    [When("I page through {string}")]
    public async Task PageThrough(string path)
    {
        Seen.Clear();
        Totals.Clear();
        _lastPath = path;
        await SendAsync(HttpMethod.Get, path);
        for (int pages = 0; pages < 100; pages++)
        {
            AssertStatus(HttpStatusCode.OK);
            JsonNode page = Json;
            Seen.AddRange(ItemsOf(page).Select(TitleOf));
            Totals.Add(page["total"]!.GetValue<long>());
            if (NextCursorOf(page) is not { } cursor)
            {
                return;
            }

            await SendAsync(HttpMethod.Get, WithCursor(path, cursor));
        }

        Assert.Fail("More than 100 pages.");
    }

    // ---------------------------------------------------------------- lists

    [Then("the titles are {string}")]
    [Then("the names are {string}")]
    public void Titles(string expected) =>
        Assert.Equal(
            Split(expected),
            ItemsOf(Json)
                .Select(TitleOf)
                .ToArray());

    [Then("the titles are, in any order, {string}")]
    public void TitlesInAnyOrder(string expected) =>
        Assert.Equal(
            Split(expected)
                .Order(StringComparer.Ordinal),
            ItemsOf(Json)
                .Select(TitleOf)
                .Order(StringComparer.Ordinal));

    [Then("{string} comes before {string}")]
    public void ComesBefore(string first, string second)
    {
        string[] titles = ItemsOf(Json)
            .Select(TitleOf)
            .ToArray();
        Assert.Contains(first, titles);
        Assert.Contains(second, titles);
        Assert.True(
            Array.IndexOf(titles, first) < Array.IndexOf(titles, second),
            $"Order: {string.Join(", ", titles)}");
    }

    [Then("there are no results")]
    public void NoResults()
    {
        Assert.Empty(ItemsOf(Json));
        Assert.Equal(0, PageOf(Json)["total"]!.GetValue<long>());
        Assert.Null(NextCursorOf(Json));
    }

    [Then("the total is {int}")]
    public void Total(int total) => Assert.Equal(total, PageOf(Json)["total"]!.GetValue<long>());

    [Then("there is a next page")]
    public void HasNextPage() => Assert.NotNull(NextCursorOf(Json));

    [Then("there is no next page")]
    public void NoNextPage() => Assert.Null(NextCursorOf(Json));

    [Then("I saw the titles {string}")]
    public void SawTitles(string expected) => Assert.Equal(Split(expected), Seen.ToArray());

    /// <summary>Relevance order isn't asserted: each title exactly once is.</summary>
    [Then("I saw, in any order, the titles {string}")]
    public void SawTitlesInAnyOrder(string expected) =>
        Assert.Equal(
            Split(expected)
                .Order(StringComparer.Ordinal),
            Seen.Order(StringComparer.Ordinal));

    [Then("every page gave the total {int}")]
    public void EveryTotal(int total) => Assert.All(Totals, t => Assert.Equal(total, t));

    [Then("the section types are {string}")]
    public void SectionTypes(string expected) =>
        Assert.Equal(
            Split(expected),
            Json["sections"]!
                .AsArray()
                .Select(s => s!["type"]!.GetValue<string>())
                .ToArray());

    [Then("the {string} section has the titles {string}")]
    public void SectionTitles(string type, string expected)
    {
        JsonNode section = Json["sections"]!
            .AsArray()
            .Single(s => s!["type"]!.GetValue<string>() == type)!;
        Assert.Equal(
            Split(expected),
            section["items"]!
                .AsArray()
                .Select(i => TitleOf(i!))
                .ToArray());
    }

    [Then("the channel names above the videos are {string}")]
    public void TopChannels(string expected) =>
        Assert.Equal(
            Split(expected),
            Json["channels"]!
                .AsArray()
                .Select(c => c!["name"]!.GetValue<string>())
                .ToArray());

    [Then("the suggestions are:")]
    public void Suggestions(DataTable table)
    {
        string[][] expected = table.Rows
            .Select(r => new[] { r["kind"], r["text"] })
            .ToArray();
        string[][] actual = Json["suggestions"]!
            .AsArray()
            .Select(s => new[] { s!["kind"]!.GetValue<string>(), s["text"]!.GetValue<string>() })
            .ToArray();
        Assert.Equal(expected, actual);
    }

    [Then("the suggestions are, in any order:")]
    public void SuggestionsInAnyOrder(DataTable table)
    {
        string[] expected = table.Rows
            .Select(r => $"{r["kind"]}: {r["text"]}")
            .Order(StringComparer.Ordinal)
            .ToArray();
        string[] actual = Json["suggestions"]!
            .AsArray()
            .Select(s => $"{s!["kind"]!.GetValue<string>()}: {s["text"]!.GetValue<string>()}")
            .Order(StringComparer.Ordinal)
            .ToArray();
        Assert.Equal(expected, actual);
    }

    // ---------------------------------------------------------------- JSON values

    /// <summary>A dotted path into the body, e.g. <c>items[0].channel.thumbnailUrl</c>.</summary>
    [Then("{string} is {string}")]
    public void ValueIs(string path, string expected) =>
        Assert.Equal(TestIds.Expand(expected), Scalar(At(path)));

    [Then("{string} has {int} item(s)")]
    public void ValueHasItems(string path, int count) => Assert.Equal(count, At(path)!.AsArray().Count);

    [Then("{string} is null")]
    public void ValueIsNull(string path) => Assert.Null(At(path));

    [Then("{string} is not null")]
    public void ValueIsNotNull(string path) => Assert.NotNull(At(path));

    [Then("{string} lists {string}")]
    public void ValueLists(string path, string expected) =>
        Assert.Equal(
            Split(expected),
            At(path)!
                .AsArray()
                .Select(Scalar)
                .ToArray());

    /// <summary>
    /// The front generates its API types from a committed copy of the document (QA10 M5-4): a backend change that
    /// alters the document fails here until the copy is refreshed.
    /// </summary>
    private static readonly System.Text.Json.JsonSerializerOptions Indented = new() { WriteIndented = true };

    [Then("the answer is the front's copy of the OpenAPI document")]
    public void IsFrontCopy()
    {
        string copy = FrontOpenApiCopy();
        JsonNode served = Json;
        if (!JsonNode.DeepEquals(served, JsonNode.Parse(File.ReadAllText(copy))))
        {
            string fresh = Path.Combine(Path.GetTempPath(), "tontuyau-openapi.json");
            File.WriteAllText(fresh, served.ToJsonString(Indented));
            Assert.Fail(
                $"{copy} differs from what the backend serves (saved in {fresh}). Refresh it: start the backend, then "
                + "run `pnpm run api:openapi` in frontend/ (it regenerates schema.d.ts too).");
        }
    }

    [Then("the parameter {string} of {string} takes {string}")]
    public void ParameterTakes(string name, string path, string values)
    {
        JsonNode parameter = Json["paths"]![path]!["get"]!["parameters"]!
            .AsArray()
            .Single(p => p!["name"]!.GetValue<string>() == name)!;
        Assert.Equal(
            Split(values),
            parameter["schema"]!["enum"]!
                .AsArray()
                .Select(Scalar)
                .ToArray());
    }

    [Then("{string} lists the widths {string}")]
    public void ListsWidths(string path, string expected) =>
        Assert.Equal(
            Split(expected),
            At(path)!
                .AsArray()
                .Select(i => Scalar(i!["width"]))
                .ToArray());

    [Then("the body is a WebP image {int} px wide")]
    public void BodyIsWebp(int width)
    {
        Assert.Equal("RIFF", Encoding.ASCII.GetString(state.ResponseBody, 0, 4));
        Assert.Equal("WEBP", Encoding.ASCII.GetString(state.ResponseBody, 8, 4));
        using SKCodec codec = SKCodec.Create(new MemoryStream(state.ResponseBody));
        Assert.Equal(width, codec.Info.Width);
    }

    [Then("the {string} of the channel {string} is {string}")]
    public void ChannelValue(string path, string name, string expected)
    {
        JsonNode channel = ItemsOf(Json).Single(c => c["name"]!.GetValue<string>() == name);
        Assert.Equal(expected, Scalar(At(channel, path)));
    }

    [Then("each item has exactly the fields {string}")]
    public void ItemFields(string expected)
    {
        foreach (JsonNode item in ItemsOf(Json))
        {
            Assert.Equal(
                Split(expected),
                item
                    .AsObject()
                    .Select(kv => kv.Key)
                    .ToArray());
        }
    }

    [Then("{string} has exactly the fields {string}")]
    public void FieldsAt(string path, string expected) =>
        Assert.Equal(
            Split(expected),
            At(path)!
                .AsObject()
                .Select(kv => kv.Key)
                .ToArray());

    [Then("there are {int} channel suggestions first, then {int} video suggestions")]
    public void SuggestionKinds(int channels, int videos)
    {
        string[] kinds = Json["suggestions"]!
            .AsArray()
            .Select(s => s!["kind"]!.GetValue<string>())
            .ToArray();
        Assert.Equal(
            [.. Enumerable.Repeat("channel", channels), .. Enumerable.Repeat("video", videos)],
            kinds);
    }

    [Then("the answer has exactly the fields {string}")]
    public void Fields(string expected) =>
        Assert.Equal(
            Split(expected),
            Json
                .AsObject()
                .Select(kv => kv.Key)
                .ToArray());

    // ---------------------------------------------------------------- errors

    [Then("the answer is a problem {int} about {string}")]
    public void ProblemAbout(int status, string field)
    {
        AssertProblem(status);
        Assert.NotNull(Json["errors"]?[field]);
    }

    [Then("the answer is a problem {int}")]
    public void Problem(int status) => AssertProblem(status);

    [Then("the problem says {string}")]
    public void ProblemSays(string text) =>
        Assert.Contains(TestIds.Expand(text), Json["detail"]!.GetValue<string>(), StringComparison.Ordinal);

    // ---------------------------------------------------------------- files

    [Then("the header {string} is {string}")]
    public void HeaderIs(string name, string expected) => Assert.Equal(TestIds.Expand(expected), Header(name));

    [Then("the header {string} is:")]
    public void HeaderIsText(string name, string expected) => HeaderIs(name, expected.Trim());

    [Then("the header {string} is missing")]
    public void HeaderMissing(string name) => Assert.Null(Header(name));

    [Then("the header {string} is there")]
    public void HeaderThere(string name) => Assert.NotNull(Header(name));

    [Then("the body is the whole file of the video {string}")]
    public async Task WholeFile(string key) =>
        Assert.Equal(await File.ReadAllBytesAsync(library.FileOf(key)), state.ResponseBody);

    [Then("the body is bytes {int} to {int} of the file")]
    public void BodyBytes(int start, int end) =>
        Assert.Equal(LibrarySteps.Content(start, end - start + 1), state.ResponseBody);

    [Then("the body is empty")]
    public void BodyEmpty() => Assert.Empty(state.ResponseBody);

    [Then("the body contains {string}")]
    public void BodyContains(string text) =>
        Assert.Contains(text, Encoding.UTF8.GetString(state.ResponseBody), StringComparison.Ordinal);

    /// <summary>A fragmented MP4 (C3): an init segment (ftyp, moov) or a part (moof, mdat).</summary>
    [Then("the body is MP4 starting with the box {string}")]
    public void BodyIsMp4(string box) =>
        Assert.Equal(box, Encoding.ASCII.GetString(state.ResponseBody, 4, 4));

    [Then("the body doesn't contain {string}")]
    public void BodyDoesNotContain(string text) =>
        Assert.DoesNotContain(text, Encoding.UTF8.GetString(state.ResponseBody), StringComparison.Ordinal);

    [Then("the body is the thumbnail of the video {string}")]
    public void BodyIsThumbnail(string key) =>
        Assert.Equal(File.ReadAllBytes(library.ArtFileOf(library.VideoOf(key).ThumbnailPath)), state.ResponseBody);

    /// <summary>The ETag of the last answer, sent back as If-None-Match.</summary>
    [When("I request {string} again with its ETag")]
    public Task AgainWithETag(string path) =>
        SendAsync(HttpMethod.Get, path, $"If-None-Match: {Header("ETag")}");

    [When("I request {string} with an If-Range of another version")]
    public Task OtherVersion(string path) =>
        SendAsync(HttpMethod.Get, path, "Range: bytes=0-9", "If-Range: \"0-0\"");

    [When("I request {string} with the range {string} and its ETag as If-Range")]
    public Task SameVersion(string path, string range) =>
        SendAsync(HttpMethod.Get, path, $"Range: {range}", $"If-Range: {Header("ETag")}");

    // ---------------------------------------------------------------- helpers

    private List<string> Seen { get; } = [];

    private List<long> Totals { get; } = [];

    /// <summary>Without a session nor a key: what a visitor sends (C3: the player closing its viewing).</summary>
    [When("I send {word} {string}")]
    public Task Send(string method, string path) => SendAsync(new HttpMethod(method), path);

    private async Task SendAsync(HttpMethod method, string path, params string[] headers)
    {
        Uri uri = new(TestIds.Expand(path), UriKind.Relative);
        using HttpRequestMessage request = new(method, uri);
        foreach (string header in headers)
        {
            int colon = header.IndexOf(':', StringComparison.Ordinal);
            request.Headers.TryAddWithoutValidation(header[..colon].Trim(), header[(colon + 1)..].Trim());
        }

        await state.SendAsync(request);
    }

    private void AssertStatus(HttpStatusCode status) =>
        Assert.True(
            state.Response!.StatusCode == status,
            $"{(int)state.Response.StatusCode}: {Encoding.UTF8.GetString(state.ResponseBody)}");

    private void AssertProblem(int status)
    {
        AssertStatus((HttpStatusCode)status);
        Assert.Equal("application/problem+json", state.Response!.Content.Headers.ContentType?.MediaType);
        Assert.Equal(status, Json["status"]!.GetValue<int>());
        Assert.StartsWith("https://tontuyau/errors/", Json["type"]!.GetValue<string>(), StringComparison.Ordinal);
        Assert.False(string.IsNullOrEmpty(Json["title"]?.GetValue<string>()));
    }

    private string? Header(string name)
    {
        HttpResponseMessage response = state.Response!;
        if (response.Headers.TryGetValues(name, out IEnumerable<string>? values)
            || response.Content.Headers.TryGetValues(name, out values))
        {
            return string.Join(", ", values);
        }

        return null;
    }

    /// <summary>The value at a path such as <c>tasks[2].waiting.runId</c> in the last answer.</summary>
    public JsonNode? At(string path) => At(Json, path);

    private static JsonNode? At(JsonNode root, string path)
    {
        JsonNode? node = root;
        foreach (string part in path.Split('.'))
        {
            int bracket = part.IndexOf('[', StringComparison.Ordinal);
            string name = bracket < 0 ? part : part[..bracket];
            if (name.Length > 0)
            {
                node = node?[name];
            }

            if (bracket >= 0)
            {
                int index = int.Parse(part[(bracket + 1)..^1], CultureInfo.InvariantCulture);
                node = node?[index];
            }
        }

        return node;
    }

    public static string Scalar(JsonNode? node) => node switch
    {
        null => "null",
        JsonValue value when value.TryGetValue(out string? text) => text,
        _ => node.ToJsonString(),
    };

    /// <summary>A page is the body itself, or its <c>videos</c> part for <c>kind=all</c>.</summary>
    private static JsonNode PageOf(JsonNode body) => body["items"] is null && body["videos"] is { } videos
        ? videos
        : body;

    private static IEnumerable<JsonNode> ItemsOf(JsonNode body) => PageOf(body)["items"]!
        .AsArray()
        .OfType<JsonNode>();

    private static string? NextCursorOf(JsonNode body) => PageOf(body)["nextCursor"]?.GetValue<string>();

    private static string TitleOf(JsonNode item) => (item["title"] ?? item["name"])!.GetValue<string>();

    private static string WithCursor(string path, string cursor) =>
        $"{path}{(path.Contains('?', StringComparison.Ordinal) ? '&' : '?')}cursor={Uri.EscapeDataString(cursor)}";

    private static string[] Split(string list) =>
        list.Length == 0
            ? []
            : list.Split(", ", StringSplitOptions.TrimEntries);

    /// <summary>frontend/src/lib/api/openapi.json, found from the test's folder up to the repository.</summary>
    private static string FrontOpenApiCopy()
    {
        for (DirectoryInfo? folder = new(AppContext.BaseDirectory); folder is not null; folder = folder.Parent)
        {
            string candidate = Path.Combine(folder.FullName, "frontend", "src", "lib", "api", "openapi.json");
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        throw new FileNotFoundException("frontend/src/lib/api/openapi.json isn't above " + AppContext.BaseDirectory);
    }
}
