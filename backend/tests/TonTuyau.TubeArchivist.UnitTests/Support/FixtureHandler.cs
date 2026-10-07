using System.Net;
using System.Text;
using System.Text.Json;

namespace TonTuyau.TubeArchivist.UnitTests.Support;

/// <summary>
/// Fake TubeArchivist: answers with recorded (anonymised) fixtures, matched on path + query and on whether the
/// request carries a token. Records every request. Unknown requests fail loudly.
/// </summary>
internal sealed class FixtureHandler : HttpMessageHandler
{
    public const string FixtureSet = "ta-v0.5.12";

    private static readonly Lazy<RecordedFixture[]> All = new(Load);

    private readonly Func<HttpRequestMessage, HttpResponseMessage?>? _override;

    public FixtureHandler(Func<HttpRequestMessage, HttpResponseMessage?>? overrideResponse = null)
    {
        _override = overrideResponse;
    }

    public List<HttpRequestMessage> Requests { get; } = [];

    public static RecordedFixture Get(string name) =>
        All.Value.SingleOrDefault(f => f.Name == name)
            ?? throw new InvalidOperationException($"No fixture named {name}.");

    /// <summary>Respond to anything with one fixture's status and body.</summary>
    public static HttpResponseMessage Respond(string fixtureName) => Get(fixtureName).ToResponse();

    public static HttpResponseMessage Respond(
        HttpStatusCode status,
        string body,
        string contentType = "application/json") =>
        new(status) { Content = new StringContent(body, Encoding.UTF8, contentType) };

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        Requests.Add(request);
        HttpResponseMessage? response = _override?.Invoke(request);
        if (response is null)
        {
            string key = request.RequestUri!.PathAndQuery;
            bool authenticated = request.Headers.Authorization is not null;
            RecordedFixture fixture = All.Value
                .FirstOrDefault(f => f.Request == key && f.Authenticated == authenticated)
                ?? throw new InvalidOperationException(
                    $"No fixture for {request.Method} {key} (authenticated: {authenticated}).");
            response = fixture.ToResponse();
        }

        response.RequestMessage = request;
        return Task.FromResult(response);
    }

    private static RecordedFixture[] Load()
    {
        string dir = Path.Combine(AppContext.BaseDirectory, "Fixtures", FixtureSet);
        return Directory
            .GetFiles(dir, "*.json")
            .Select(path =>
            {
                using JsonDocument doc = JsonDocument.Parse(File.ReadAllText(path));
                JsonElement root = doc.RootElement;
                JsonElement json = root.GetProperty("json");
                return new RecordedFixture(
                    Path.GetFileNameWithoutExtension(path),
                    root.GetProperty("request").GetString()!,
                    root.GetProperty("authenticated").GetBoolean(),
                    root.GetProperty("status").GetInt32(),
                    root.GetProperty("contentType").GetString() ?? "text/plain",
                    json.ValueKind == JsonValueKind.Null ? root.GetProperty("text").GetString()! : json.GetRawText());
            })
            .ToArray();
    }
}

internal sealed record RecordedFixture(
    string Name,
    string Request,
    bool Authenticated,
    int Status,
    string ContentType,
    string Body)
{
    public HttpResponseMessage ToResponse() => FixtureHandler.Respond((HttpStatusCode)Status, Body, ContentType);

    public JsonElement Json => JsonDocument.Parse(Body).RootElement;
}
