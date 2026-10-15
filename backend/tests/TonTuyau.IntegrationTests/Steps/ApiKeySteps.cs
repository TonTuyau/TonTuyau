using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TonTuyau.Application.ApiKeys;
using TonTuyau.Infrastructure.Accounts;
using TonTuyau.Infrastructure.ApiKeys;
using TonTuyau.IntegrationTests.Support;

namespace TonTuyau.IntegrationTests.Steps;

/// <summary>
/// The API keys (1.5): made and managed with the admin's session (<see cref="AuthSteps"/>), used like a program
/// would, as <c>Authorization: Bearer</c>. The secrets of the keys made in the scenario are kept by name.
/// </summary>
[Binding]
public sealed class ApiKeySteps(ScenarioState state, AuthSteps auth)
{
    private readonly Dictionary<string, string> _secrets = new(StringComparer.Ordinal);

    private JsonNode Json => JsonNode.Parse(Encoding.UTF8.GetString(state.ResponseBody))
        ?? throw new InvalidOperationException("The body is JSON null.");

    [When("I create the API key {string}")]
    public Task Create(string name) => CreateOfKind(name, "browserExtension");

    /// <summary>An empty kind is sent as no kind at all.</summary>
    [When("I create the API key {string} of kind {string}")]
    public async Task CreateOfKind(string name, string kind)
    {
        string body = JsonSerializer.Serialize(new { name, kind = kind.Length == 0 ? null : kind });
        using HttpRequestMessage request = new(HttpMethod.Post, new Uri("/api/admin/api-keys", UriKind.Relative))
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json"),
        };
        await auth.SendAsync(request);
        if (state.Response!.StatusCode == HttpStatusCode.Created)
        {
            _secrets[name] = Json["secret"]!.GetValue<string>();
        }
    }

    [Given("the API key {string} exists")]
    public async Task Exists(string name)
    {
        await auth.LogInAsAdmin();
        await Create(name);
        Assert.Equal(HttpStatusCode.Created, state.Response!.StatusCode);
    }

    [When("I list the API keys with my session")]
    public async Task List()
    {
        using HttpRequestMessage request = new(HttpMethod.Get, new Uri("/api/admin/api-keys", UriKind.Relative));
        await auth.SendAsync(request);
    }

    [When("I enable the API key {string}")]
    public Task Enable(string name) => ManageAsync("enable", name);

    [When("I disable the API key {string}")]
    public Task Disable(string name) => ManageAsync("disable", name);

    [When("I delete the API key {string}")]
    public Task Delete(string name) => ManageAsync("delete", name);

    [When("I delete the API key {int}")]
    public async Task DeleteById(int id)
    {
        Uri path = new($"/api/admin/api-keys/{id}", UriKind.Relative);
        using HttpRequestMessage request = new(HttpMethod.Delete, path);
        await auth.SendAsync(request);
    }

    [When("I send {word} {string} with the API key {string}")]
    public Task SendWithKey(string method, string path, string name) =>
        SendWithBearerAsync(new HttpMethod(method), path, Secret(name));

    [When("I send {word} {string} with the bearer {string}")]
    public Task SendWithBearer(string method, string path, string bearer) =>
        SendWithBearerAsync(new HttpMethod(method), path, bearer);

    [Then("the new key's secret is {string} and {int} random characters")]
    public void SecretShape(string start, int length)
    {
        string secret = Json["secret"]!.GetValue<string>();
        Assert.StartsWith(start, secret, StringComparison.Ordinal);
        Assert.Equal(start.Length + length, secret.Length);
        Assert.Matches("^[A-Za-z0-9]+$", secret[start.Length..]);
        Assert.Equal(secret[..ApiKeyService.PrefixLength], Json["key"]!["prefix"]!.GetValue<string>());
    }

    /// <summary>The names, latest first, separated by " | " (a name may hold a comma).</summary>
    [Then("the API keys are {string}")]
    public async Task Names(string expected)
    {
        await List();
        Assert.Equal(HttpStatusCode.OK, state.Response!.StatusCode);
        Assert.Equal(
            expected.Length == 0 ? [] : expected.Split(" | "),
            Json
                .AsArray()
                .Select(k => k!["name"]!.GetValue<string>())
                .ToArray());
    }

    [Then("no answer about API keys holds a secret")]
    public async Task NoSecretListed()
    {
        await List();
        Assert.All(_secrets.Values, s => Assert.DoesNotContain(s, Encoding.UTF8.GetString(state.ResponseBody)));
        Assert.All(Json.AsArray(), k => Assert.Null(k!["secret"]));
    }

    [Then("the database keeps only the hash of the API key {string}")]
    public async Task OnlyTheHash(string name)
    {
        await using AsyncServiceScope scope = state.Backend.Services.CreateAsyncScope();
        TonTuyauDbContext db = scope.ServiceProvider.GetRequiredService<TonTuyauDbContext>();
        ApiKeyRow row = await db.ApiKeys.SingleAsync(k => k.Name == name);
        string secret = Secret(name);
        Assert.Equal(ApiKeyService.HashOf(secret), row.Hash);
        Assert.DoesNotContain(
            secret,
            new[] { row.Name, row.NameKey, row.Prefix, row.Hash, row.CreatedBy });
    }

    [Then("the API key {string} is {word}, last used {word}")]
    public async Task KeyState(string name, string enabled, string lastUsed)
    {
        JsonNode key = await KeyAsync(name);
        Assert.Equal(enabled == "enabled", key["enabled"]!.GetValue<bool>());
        Assert.Equal(lastUsed == "never", key["lastUsedAt"] is null);
    }

    private async Task ManageAsync(string action, string name)
    {
        int id = await IdOfAsync(name);
        (HttpMethod method, string path) = action switch
        {
            "enable" => (HttpMethod.Post, $"/api/admin/api-keys/{id}/enable"),
            "disable" => (HttpMethod.Post, $"/api/admin/api-keys/{id}/disable"),
            "delete" => (HttpMethod.Delete, $"/api/admin/api-keys/{id}"),
            _ => throw new ArgumentOutOfRangeException(nameof(action), action, "enable, disable or delete."),
        };
        using HttpRequestMessage request = new(method, new Uri(path, UriKind.Relative));
        await auth.SendAsync(request);
    }

    private async Task SendWithBearerAsync(HttpMethod method, string path, string bearer)
    {
        using HttpRequestMessage request = new(method, new Uri(TestIds.Expand(path), UriKind.Relative));
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearer);
        await state.SendAsync(request);
    }

    private string Secret(string name) =>
        _secrets.TryGetValue(name, out string? secret)
            ? secret
            : throw new InvalidOperationException($"No key “{name}” was created in this scenario.");

    private async Task<int> IdOfAsync(string name) =>
        (await KeyAsync(name))["id"]!.GetValue<int>();

    private async Task<JsonNode> KeyAsync(string name)
    {
        await List();
        Assert.Equal(HttpStatusCode.OK, state.Response!.StatusCode);
        return Json
            .AsArray()
            .Single(k => k!["name"]!.GetValue<string>() == name)!;
    }
}
