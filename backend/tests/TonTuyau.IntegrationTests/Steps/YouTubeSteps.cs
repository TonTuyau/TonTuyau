using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TonTuyau.Application.Configuration;
using TonTuyau.Infrastructure.Accounts;
using TonTuyau.Infrastructure.YouTube;
using TonTuyau.IntegrationTests.Support;

namespace TonTuyau.IntegrationTests.Steps;

/// <summary>
/// The YouTube cookie (1.5): sent by the extension (an API key) or an admin, checked by a fake yt-dlp (FakeTools/)
/// that answers by the cookie's value: SIGNED_IN works, BOT_CHECK gets a bot check, anything else is refused.
/// </summary>
[Binding]
public sealed class YouTubeSteps(ScenarioState state, AuthSteps auth, ApiKeySteps keys)
{
    private static readonly string FakeTools = Path.Combine(AppContext.BaseDirectory, "FakeTools");

    private string? _folder;

    /// <summary>What the fake yt-dlp answers for a video or channel: <c>&lt;id&gt;.json</c> or <c>&lt;id&gt;.err</c>.</summary>
    public string Answers => Path.Combine(_folder!, "answers");

    /// <summary>A folder of the scenario's own, with a link to the fake yt-dlp and its answers next to it.</summary>
    [BeforeScenario("youtube")]
    public void UseFakeTools()
    {
        _folder = Path.Combine(Path.GetTempPath(), "tontuyau-yt-dlp-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Answers);
        string ytdlp = Path.Combine(_folder, "yt-dlp");
        File.CreateSymbolicLink(ytdlp, Path.Combine(FakeTools, "yt-dlp"));
        state.Settings.Set(ConfigKeys.EnvironmentName(ConfigKeys.YtdlpPath), ytdlp);
        state.Settings.Set(ConfigKeys.EnvironmentName(ConfigKeys.DenoPath), Path.Combine(FakeTools, "deno"));
    }

    [AfterScenario("youtube")]
    public void RemoveFakeTools()
    {
        if (_folder is not null && Directory.Exists(_folder))
        {
            Directory.Delete(_folder, recursive: true);
        }
    }

    /// <summary>A browser's cookie file: a Google cookie, and the YouTube session whose value is given.</summary>
    public static string CookieFile(string value) =>
        "# Netscape HTTP Cookie File\n"
        + ".google.com\tTRUE\t/\tTRUE\t1820000000\tSID\tgoogle-secret\n"
        + $".youtube.com\tTRUE\t/\tTRUE\t1820000000\tLOGIN_INFO\t{value}\n";

    [Given("the extension sent the YouTube cookie {string} with the API key {string}")]
    [When("the extension sends the YouTube cookie {string} with the API key {string}")]
    public async Task SendWithKey(string value, string name)
    {
        using HttpRequestMessage request = CookieRequest(CookieFile(value));
        await keys.SendWithKeyAsync(request, name);
    }

    [Given("I sent the YouTube cookie {string} with my session")]
    [When("I send the YouTube cookie {string} with my session")]
    public async Task SendWithSession(string value)
    {
        using HttpRequestMessage request = CookieRequest(CookieFile(value));
        await auth.SendAsync(request);
    }

    [When("I send the raw cookie file {string} with my session")]
    public async Task SendRaw(string text)
    {
        using HttpRequestMessage request = CookieRequest(text);
        await auth.SendAsync(request);
    }

    [When("I check the YouTube cookie with my session")]
    public async Task Check()
    {
        Uri path = new("/api/admin/youtube/cookie/check", UriKind.Relative);
        using HttpRequestMessage request = new(HttpMethod.Post, path);
        await auth.SendAsync(request);
    }

    [When("I remove the YouTube cookie with my session")]
    public async Task Remove()
    {
        Uri path = new("/api/admin/youtube/cookie", UriKind.Relative);
        using HttpRequestMessage request = new(HttpMethod.Delete, path);
        await auth.SendAsync(request);
    }

    [Then("the database holds only the youtube.com cookie {string}")]
    public async Task OnlyYouTube(string value)
    {
        await using AsyncServiceScope scope = state.Backend.Services.CreateAsyncScope();
        YouTubeCookieRow row = await scope.ServiceProvider
            .GetRequiredService<TonTuyauDbContext>()
            .YouTubeCookie
            .SingleAsync();
        Assert.Equal(
            $"# Netscape HTTP Cookie File\n.youtube.com\tTRUE\t/\tTRUE\t1820000000\tLOGIN_INFO\t{value}\n",
            row.Content);
    }

    [Then("the answer doesn't hold the cookie's content")]
    public void NoContent()
    {
        string body = Encoding.UTF8.GetString(state.ResponseBody);
        Assert.Equal(HttpStatusCode.OK, state.Response!.StatusCode);
        Assert.DoesNotContain("LOGIN_INFO", body, StringComparison.Ordinal);
        Assert.DoesNotContain("SIGNED_IN", body, StringComparison.Ordinal);
    }

    private static HttpRequestMessage CookieRequest(string text) =>
        new(HttpMethod.Put, new Uri("/api/extension/youtube-cookie", UriKind.Relative))
        {
            Content = new StringContent(
                JsonSerializer.Serialize(new { cookies = text }),
                Encoding.UTF8,
                "application/json"),
        };
}
