using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Net.Http.Headers;
using TonTuyau.Api.Hosting;
using TonTuyau.Application.Accounts;
using TonTuyau.Infrastructure.Accounts;
using TonTuyau.IntegrationTests.Support;

namespace TonTuyau.IntegrationTests.Steps;

/// <summary>
/// Logs in and out like a browser would: the session cookie of the last answer that set one is sent with the next
/// requests (until an answer deletes it).
/// </summary>
[Binding]
public sealed partial class AuthSteps(ScenarioState state)
{
    private const string AdminName = "admin";

    private readonly Dictionary<string, string?> _kept = new(StringComparer.Ordinal);

    private string? _cookie;
    private SetCookieHeaderValue? _lastSetCookie;

    [When("I log in as {string} with {string}")]
    public Task LogIn(string username, string password) => LogInAsync(username, password, remember: false, from: null);

    [When("I log in as {string} with {string}, staying logged in")]
    public Task LogInRemembered(string username, string password) =>
        LogInAsync(username, password, remember: true, from: null);

    [Given("I logged in as the admin")]
    [When("I log in as the admin")]
    public Task LogInAsAdmin() => LogInAsync(AdminName, TestSettings.AdminPassword, remember: false, from: null);

    [When("I log in as the admin from {string}")]
    public Task LogInAsAdminFrom(string forwardedFor) =>
        LogInAsync(AdminName, TestSettings.AdminPassword, remember: false, from: forwardedFor);

    [When("I try a wrong password {int} times from {string}")]
    public async Task WrongPasswordTimes(int times, string forwardedFor)
    {
        for (int i = 0; i < times; i++)
        {
            await LogInAsync(AdminName, "wrong-password", remember: false, from: forwardedFor);
            ThenTheLoginIsRefused();
        }
    }

    [When("I send the login {string} as {string}")]
    public Task SendRawLogin(string body, string contentType) =>
        PostAsync("/api/auth/login", new StringContent(body, Encoding.UTF8, contentType), from: null);

    [When("I ask who is logged in")]
    public async Task AskWhoIsLoggedIn()
    {
        using HttpRequestMessage request = new(HttpMethod.Get, new Uri("/api/auth/me", UriKind.Relative));
        await SendAsync(request);
    }

    /// <summary>A GET with the session cookie, if there is one (<c>{video:key}</c> etc. as in the other requests).</summary>
    [When("I request {string} with my session")]
    public async Task RequestWithSession(string path)
    {
        using HttpRequestMessage request = new(HttpMethod.Get, new Uri(TestIds.Expand(path), UriKind.Relative));
        await SendAsync(request);
    }


    /// <summary>A JSON request with the session cookie (2.3.0: the accounts), the body as written.</summary>
    [When("I send {word} {string} with my session and {string}")]
    public async Task SendJsonWithSession(string method, string path, string json)
    {
        Uri uri = new(await ExpandAsync(path), UriKind.Relative);
        using HttpRequestMessage request = new(new HttpMethod(method), uri)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json"),
        };
        await SendAsync(request);
    }

    public Task PostWithSession(string path) => PostAsync(TestIds.Expand(path), content: null, from: null);

    /// <summary>A request without a body (POST, DELETE…) with the session cookie.</summary>
    [When("I send {word} {string} with my session")]
    public async Task SendWithSession(string method, string path)
    {
        Uri uri = new(await ExpandAsync(path), UriKind.Relative);
        using HttpRequestMessage request = new(new HttpMethod(method), uri);
        await SendAsync(request);
    }

    /// <summary>A local user (2.3.0), made as the admin's accounts page makes one.</summary>
    [Given("there is the user {string} with the password {string}")]
    public async Task ThereIsTheUser(string username, string password)
    {
        await using AsyncServiceScope scope = state.Backend.Services.CreateAsyncScope();
        await scope.ServiceProvider
            .GetRequiredService<AccountService>()
            .CreateAsync(username, password, "user", "test", TestContext.Current.CancellationToken);
    }

    /// <summary>Another browser (2.3.0): this session is kept aside under a name, and the next requests go without one.</summary>
    [Given("I keep this session as {string}")]
    [When("I keep this session as {string}")]
    public void KeepSession(string name)
    {
        _kept[name] = _cookie;
        _cookie = null;
    }

    [When("I go back to the session {string}")]
    public void UseSession(string name) =>
        _cookie = _kept.TryGetValue(name, out string? cookie)
            ? cookie
            : throw new InvalidOperationException($"No session kept as {name}.");

    [When("I log out")]
    public Task LogOut() => PostAsync("/api/auth/logout", content: null, from: null);

    [Then("I am logged in as {string}")]
    public void ThenIAmLoggedInAs(string username)
    {
        Assert.Equal(HttpStatusCode.OK, state.Response!.StatusCode);
        using JsonDocument body = JsonDocument.Parse(state.ResponseBody);
        Assert.Equal(username, body.RootElement.GetProperty("username").GetString());
    }

    [Then("nobody is logged in")]
    public void ThenNobodyIsLoggedIn() => Assert.Equal(HttpStatusCode.Unauthorized, state.Response!.StatusCode);

    [Then("the login is refused")]
    public void ThenTheLoginIsRefused()
    {
        Assert.Equal(HttpStatusCode.Unauthorized, state.Response!.StatusCode);
        Assert.Equal("application/problem+json", state.Response.Content.Headers.ContentType?.MediaType);
        using JsonDocument body = JsonDocument.Parse(state.ResponseBody);
        Assert.Equal("Wrong username or password.", body.RootElement.GetProperty("detail").GetString());
        Assert.Null(_lastSetCookie);
    }

    [Then("the session cookie is HttpOnly, SameSite=Strict and Secure")]
    public void ThenTheCookieIsLockedDown()
    {
        SetCookieHeaderValue cookie = LastSetCookie();
        Assert.True(cookie.HttpOnly);
        Assert.Equal(Microsoft.Net.Http.Headers.SameSiteMode.Strict, cookie.SameSite);
        Assert.True(cookie.Secure);
        Assert.Equal("/", cookie.Path.Value);
    }

    [Then("the session cookie isn't Secure")]
    public void ThenTheCookieIsNotSecure() => Assert.False(LastSetCookie().Secure);

    [Then("the session cookie ends with the browser")]
    public void ThenTheCookieEndsWithTheBrowser()
    {
        SetCookieHeaderValue cookie = LastSetCookie();
        Assert.Null(cookie.Expires);
        Assert.Null(cookie.MaxAge);
    }

    [Then("the session cookie lasts {int} days")]
    public void ThenTheCookieLasts(int days)
    {
        DateTimeOffset? expires = LastSetCookie().Expires;
        Assert.NotNull(expires);
        Assert.InRange(
            expires.Value - DateTimeOffset.UtcNow,
            TimeSpan.FromDays(days) - TimeSpan.FromMinutes(5),
            TimeSpan.FromDays(days));
    }

    [Then("the database has {int} account(s)")]
    public async Task ThenTheDatabaseHasAccounts(int count)
    {
        await using AsyncServiceScope scope = state.Backend.Services.CreateAsyncScope();
        TonTuyauDbContext db = scope.ServiceProvider.GetRequiredService<TonTuyauDbContext>();
        Assert.Equal(count, await db.Accounts.CountAsync());
    }

    [Then("the account {string} has a last login time")]
    public async Task ThenTheAccountHasALastLogin(string username)
    {
        Assert.NotNull((await AccountAsync(username)).LastLoginAt);
    }

    [Then("the account {string} has no last login time")]
    public async Task ThenTheAccountHasNoLastLogin(string username)
    {
        Assert.Null((await AccountAsync(username)).LastLoginAt);
    }

    /// <summary>The ids of the scenario (<see cref="TestIds.Expand"/>), and <c>{account:name}</c>: that account's id.</summary>
    private async Task<string> ExpandAsync(string path)
    {
        string expanded = TestIds.Expand(path);
        foreach (Match match in AccountPlaceholder().Matches(expanded))
        {
            Account account = await AccountAsync(match.Groups["name"].Value);
            expanded = expanded.Replace(
                match.Value,
                account.Id.ToString(CultureInfo.InvariantCulture),
                StringComparison.Ordinal);
        }

        return expanded;
    }

    [GeneratedRegex("\\{account:(?<name>[^}]+)\\}")]
    private static partial Regex AccountPlaceholder();

    private async Task<Account> AccountAsync(string username)
    {
        await using AsyncServiceScope scope = state.Backend.Services.CreateAsyncScope();
        Account? account = await scope.ServiceProvider
            .GetRequiredService<IAccountRepository>()
            .FindByUsernameAsync(username, TestContext.Current.CancellationToken);
        return account ?? throw new InvalidOperationException($"No account {username}.");
    }

    private Task LogInAsync(string username, string password, bool remember, string? from)
    {
        string body = JsonSerializer.Serialize(new { username, password, remember });
        return PostAsync("/api/auth/login", new StringContent(body, Encoding.UTF8, "application/json"), from);
    }

    private async Task PostAsync(string path, HttpContent? content, string? from)
    {
        using HttpRequestMessage request = new(HttpMethod.Post, new Uri(path, UriKind.Relative)) { Content = content };
        if (from is not null)
        {
            request.Headers.Add("X-Forwarded-For", from);
        }

        await SendAsync(request);
    }

    /// <summary>Sends the cookie if there is one, and keeps whatever the answer does to it.</summary>
    public async Task SendAsync(HttpRequestMessage request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (_cookie is not null)
        {
            request.Headers.Add("Cookie", _cookie);
        }

        await state.SendAsync(request);
        _lastSetCookie = null;
        if (state.Response!.Headers.TryGetValues("Set-Cookie", out IEnumerable<string>? values))
        {
            _lastSetCookie = SetCookieHeaderValue
                .ParseList(values.ToList())
                .SingleOrDefault(c => c.Name == AuthServices.CookieName);
        }

        if (_lastSetCookie is not null)
        {
            // Deleting a cookie is setting it already expired.
            _cookie = _lastSetCookie.Expires < DateTimeOffset.UtcNow
                ? null
                : $"{_lastSetCookie.Name}={_lastSetCookie.Value}";
        }
    }

    private SetCookieHeaderValue LastSetCookie() =>
        _lastSetCookie ?? throw new InvalidOperationException("The last answer set no session cookie.");
}
