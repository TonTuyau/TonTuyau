using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.DependencyInjection;
using TonTuyau.Application.Configuration;
using TonTuyau.IntegrationTests.Support;
using YouTubeServices = TonTuyau.YouTube.ServiceCollectionExtensions;

namespace TonTuyau.IntegrationTests.Steps;

/// <summary>
/// Which yt-dlp runs (1.5): the image's copy (FakeTools/yt-dlp, 2026.08.19) until another version, from the fake
/// GitHub, is downloaded, checked, tried and switched to.
/// </summary>
[Binding]
public sealed class YtdlpSteps(ScenarioState state, AuthSteps auth) : IAsyncDisposable
{
    private FakeGitHub? _gitHub;

    private JsonNode Json => JsonNode.Parse(Encoding.UTF8.GetString(state.ResponseBody))
        ?? throw new InvalidOperationException("The body is JSON null.");

    [Given("GitHub lists the official yt-dlp releases:")]
    public async Task OfficialReleases(DataTable table)
    {
        FakeGitHub gitHub = await GitHubAsync();
        gitHub.Official.AddRange(Releases(table));
    }

    [Given("GitHub lists the nightly yt-dlp builds:")]
    public async Task NightlyBuilds(DataTable table)
    {
        FakeGitHub gitHub = await GitHubAsync();
        gitHub.Nightly.AddRange(Releases(table));
    }

    [Given("GitHub's checksum of yt-dlp {string} is wrong")]
    public async Task WrongChecksum(string version)
    {
        FakeGitHub gitHub = await GitHubAsync();
        gitHub.WrongChecksum.Add(version);
    }

    [When("I choose yt-dlp {string} {string} with my session")]
    public async Task Choose(string kind, string version)
    {
        string body = JsonSerializer.Serialize(new { kind, version = version.Length == 0 ? null : version });
        Uri path = new("/api/admin/youtube/ytdlp", UriKind.Relative);
        using HttpRequestMessage request = new(HttpMethod.Put, path)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json"),
        };
        await auth.SendAsync(request);
    }

    [Then("yt-dlp runs {string} within {int} seconds")]
    public async Task RunsWithin(string version, int seconds)
    {
        DateTime deadline = DateTime.UtcNow.AddSeconds(seconds);
        while (true)
        {
            await auth.RequestWithSession("/api/admin/youtube/ytdlp");
            if (state.Response!.StatusCode == HttpStatusCode.OK && Json["running"]?.GetValue<string>() == version)
            {
                return;
            }

            if (DateTime.UtcNow > deadline)
            {
                Assert.Fail($"Not {version} after {seconds} s: {Encoding.UTF8.GetString(state.ResponseBody)}");
            }

            await Task.Delay(200);
        }
    }

    [Then("yt-dlp's last check failed, mentioning {string}, within {int} seconds")]
    public async Task FailedWithin(string text, int seconds)
    {
        DateTime deadline = DateTime.UtcNow.AddSeconds(seconds);
        while (true)
        {
            await auth.RequestWithSession("/api/admin/youtube/ytdlp");
            if (Json["error"]?.GetValue<string>() is { } error)
            {
                Assert.Contains(text, error, StringComparison.Ordinal);
                return;
            }

            if (DateTime.UtcNow > deadline)
            {
                Assert.Fail($"No failure after {seconds} s: {Encoding.UTF8.GetString(state.ResponseBody)}");
            }

            await Task.Delay(200);
        }
    }

    [Then("yt-dlp {string} waits for the task going on within {int} seconds")]
    public async Task WaitsWithin(string version, int seconds)
    {
        DateTime deadline = DateTime.UtcNow.AddSeconds(seconds);
        while (true)
        {
            await auth.RequestWithSession("/api/admin/youtube/ytdlp");
            if (Json["waiting"]?.GetValue<string>() == version)
            {
                return;
            }

            if (DateTime.UtcNow > deadline)
            {
                Assert.Fail($"No {version} waiting after {seconds} s: {Encoding.UTF8.GetString(state.ResponseBody)}");
            }

            await Task.Delay(200);
        }
    }

    [Then("the yt-dlp versions offered are {string} and the nightly builds {string}")]
    public void Offered(string official, string nightly)
    {
        Assert.Equal(Split(official), Versions("official"));
        Assert.Equal(Split(nightly), Versions("nightly"));
    }

    /// <summary>
    /// Within a few seconds: the backend switches to the new version first, then removes the others (never one still
    /// in use), so the page can say it runs a moment before the old program is gone.
    /// </summary>
    [Then("the tools folder holds yt-dlp {string} only")]
    public async Task ToolsHold(string version)
    {
        string root = state.Settings.Get(ConfigKeys.EnvironmentName(ConfigKeys.ToolsRoot))!;
        string?[] expected = version.Length == 0 ? [] : [$"yt-dlp-{version}"];
        string?[] Held() => Directory
            .EnumerateFiles(root, "yt-dlp-*")
            .Select(Path.GetFileName)
            .ToArray();
        DateTime deadline = DateTime.UtcNow.AddSeconds(5);
        while (!Held().SequenceEqual(expected) && DateTime.UtcNow < deadline)
        {
            await Task.Delay(100);
        }

        Assert.Equal(expected, Held());
    }

    [Then("yt-dlp {string} was downloaded {int} time(s)")]
    public void Downloaded(string version, int times) => Assert.Equal(times, _gitHub!.Downloads(version));

    public async ValueTask DisposeAsync()
    {
        if (_gitHub is not null)
        {
            await _gitHub.DisposeAsync();
        }
    }

    private static string[] Split(string list) => list.Length == 0 ? [] : list.Split(", ");

    private static IEnumerable<FakeRelease> Releases(DataTable table) =>
        table.Rows.Select(r => new FakeRelease(
            r["version"],
            DateTimeOffset.UtcNow.AddDays(-int.Parse(r["days ago"], CultureInfo.InvariantCulture)),
            !r.TryGetValue("linux", out string? linux) || linux != "no"));

    private string[] Versions(string channel) =>
        [.. Json[channel]!
            .AsArray()
            .Select(v => v!["version"]!.GetValue<string>())];

    /// <summary>The fake GitHub, started once, before the backend (whose GitHub client is pointed there).</summary>
    private async Task<FakeGitHub> GitHubAsync()
    {
        if (_gitHub is null)
        {
            _gitHub = await FakeGitHub.StartAsync();
            Uri fake = new(_gitHub.Url);
            state.ServiceOverrides.Add(services => services
                .AddHttpClient(YouTubeServices.GitHubClientName)
                .ConfigurePrimaryHttpMessageHandler(() => new OutsideRedirect("GitHub", fake)));
        }

        return _gitHub;
    }
}
