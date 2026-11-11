using System.Text.Json.Nodes;
using Microsoft.Extensions.DependencyInjection;
using TonTuyau.Application.Accounts;
using TonTuyau.IntegrationTests.Support;

namespace TonTuyau.IntegrationTests.Steps;

/// <summary>
/// The LDAP directory (2.3.0): a real LLDAP (<see cref="LldapContainer"/>), reset before each scenario that turns it
/// on; set as the admin's LDAP page sets it, with the user's own values (Jellyfin's).
/// </summary>
[Binding]
public sealed class LdapSteps(ScenarioState state, AuthSteps auth)
{
    public const string ResetUrl = "http://ldap.example.com:17170/$username/password";

    private string? _url;

    [Given("the LDAP directory is on")]
    public async Task DirectoryOn()
    {
        _url = await LldapContainer.ResetAsync();
        await SaveAsync(Settings(_url, LldapContainer.PasswordOf("tontuyau")));
    }

    /// <summary>On, but nothing answers at its address (a directory down, a wrong host).</summary>
    [Given("the LDAP directory is on but doesn't answer")]
    public Task DirectoryDown() =>
        SaveAsync(Settings("ldap://127.0.0.1:1", LldapContainer.PasswordOf("tontuyau")));

    [Given("{string} is taken out of the directory's users group")]
    [When("{string} is taken out of the directory's users group")]
    public static Task OutOfUsers(string uid) => LldapContainer.RemoveFromGroupAsync(uid, LldapContainer.UsersGroup);

    /// <summary>The settings as the LDAP page sends them, the service account's password typed (or not).</summary>
    [When("I save the LDAP settings with my session")]
    public Task SaveWithSession() =>
        SendSettingsAsync("PUT", "/api/admin/settings/ldap", withPassword: true, test: null);

    [When("I save the LDAP settings without the service account's password with my session")]
    public Task SaveWithoutPassword() =>
        SendSettingsAsync("PUT", "/api/admin/settings/ldap", withPassword: false, test: null);

    [When("I run the LDAP test {string} with my session")]
    public Task Test(string kind) =>
        SendSettingsAsync(
            "POST",
            "/api/admin/settings/ldap/test",
            withPassword: false,
            test: new() { ["test"] = kind });

    [When("I look up {string} in the LDAP directory with my session")]
    public Task LookUp(string username) =>
        SendSettingsAsync(
            "POST",
            "/api/admin/settings/ldap/test",
            withPassword: false,
            test: new() { ["test"] = "name", ["username"] = username });

    [When("I run the LDAP test {string} with the service account's password {string} with my session")]
    public Task TestWithPassword(string kind, string password) =>
        SendSettingsAsync(
            "POST",
            "/api/admin/settings/ldap/test",
            withPassword: false,
            test: new() { ["test"] = kind, ["bindPassword"] = password });

    [Then("the directory takes {string}'s password {string}")]
    public static async Task DirectoryTakes(string uid, string password) =>
        Assert.True(await LldapContainer.PasswordIsAsync(uid, password));

    private async Task SendSettingsAsync(string method, string path, bool withPassword, JsonObject? test)
    {
        _url ??= await LldapContainer.ResetAsync();
        JsonObject body = new()
        {
            ["enabled"] = true,
            ["url"] = _url,
            ["bindDn"] = LldapContainer.ServiceDn,
            ["baseDn"] = $"ou=people,{LldapContainer.BaseDn}",
            ["uidAttribute"] = "uid",
            ["usernameAttribute"] = "cn",
            // As the user's Jellyfin has them: a space after a comma.
            ["usersFilter"] = $"(memberof=cn={LldapContainer.UsersGroup},ou=groups, {LldapContainer.BaseDn})",
            ["adminsFilter"] = $"(memberof=cn={LldapContainer.AdminsGroup},ou=groups, {LldapContainer.BaseDn})",
            ["passwordAttribute"] = "userPassword",
            ["passwordResetUrl"] = ResetUrl,
        };
        if (withPassword)
        {
            body["bindPassword"] = LldapContainer.PasswordOf("tontuyau");
        }

        foreach ((string key, JsonNode? value) in test ?? [])
        {
            body[key] = value?.DeepClone();
        }

        await auth.SendJsonWithSession(method, path, body.ToJsonString());
    }

    private static LdapSettingsChange Settings(string url, string bindPassword) => new(
        Enabled: true,
        url,
        StartTls: false,
        CaCertificate: null,
        LldapContainer.ServiceDn,
        bindPassword,
        $"ou=people,{LldapContainer.BaseDn}",
        "uid",
        "cn",
        $"(memberof=cn={LldapContainer.UsersGroup},ou=groups,{LldapContainer.BaseDn})",
        $"(memberof=cn={LldapContainer.AdminsGroup},ou=groups,{LldapContainer.BaseDn})",
        "userPassword",
        ResetUrl);

    private async Task SaveAsync(LdapSettingsChange settings)
    {
        await state.WaitUntilReadyAsync();
        await using AsyncServiceScope scope = state.Backend.Services.CreateAsyncScope();
        await scope.ServiceProvider
            .GetRequiredService<LdapService>()
            .SaveAsync(settings, "test", TestContext.Current.CancellationToken);
    }
}
