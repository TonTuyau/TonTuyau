using System.Globalization;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using Novell.Directory.Ldap;
using TonTuyau.Infrastructure.Accounts;

namespace TonTuyau.IntegrationTests.Support;

/// <summary>
/// One real LLDAP for the whole run (2.3.0: the user's directory, its version), started by the first @ldap scenario,
/// and its directory reset before each one (<see cref="ResetAsync"/>): the scenarios of a feature run one after another,
/// and some change a password or a group. Accounts and groups through LLDAP's GraphQL API, passwords through LDAP.
/// </summary>
public static class LldapContainer
{
    public const string Image = "lldap/lldap:v0.5.0";
    public const string BaseDn = "dc=example,dc=com";
    public const string AdminPassword = "integration-lldap-admin";
    public const string ServiceDn = "uid=tontuyau,ou=people," + BaseDn;
    public const string UsersGroup = "tontuyau";
    public const string AdminsGroup = "tontuyau_admin";

    private const int LdapPort = 3890;
    private const int WebPort = 17170;

    /// <summary>
    /// The directory every scenario starts from: each account's name, shown name and groups; its password is the name
    /// with <c>-password</c>. The service account changes passwords (lldap_password_manager), but not an LLDAP admin's
    /// (boss): that one changes its own.
    /// </summary>
    private static readonly (string Uid, string Name, string[] Groups)[] People =
    [
        ("tontuyau", "TonTuyau", ["lldap_password_manager"]),
        ("eclegv", "Eclegv", [UsersGroup, AdminsGroup]),
        ("marie", "Marie Dubois", [UsersGroup]),
        ("camille", "Camille Leroy", [UsersGroup]),
        ("zoe", "Zoé Bernard", []),
        ("boss", "Big Boss", [UsersGroup, "lldap_admin"]),
    ];

    private static readonly SemaphoreSlim Gate = new(1, 1);
    private static IContainer? _container;

    public static string PasswordOf(string uid) => uid + "-password";

    /// <summary><c>ldap://host:port</c>, the directory reset to <see cref="People"/>.</summary>
    public static async Task<string> ResetAsync()
    {
        await Gate.WaitAsync().ConfigureAwait(false);
        try
        {
            if (_container is null)
            {
                IContainer container = new ContainerBuilder(Image)
                    .WithEnvironment("LLDAP_JWT_SECRET", "integration-jwt-secret-0123456789")
                    .WithEnvironment("LLDAP_KEY_SEED", "integration-key-seed-0123456789")
                    .WithEnvironment("LLDAP_LDAP_USER_PASS", AdminPassword)
                    .WithEnvironment("LLDAP_LDAP_BASE_DN", BaseDn)
                    .WithPortBinding(LdapPort, true)
                    .WithPortBinding(WebPort, true)
                    .WithWaitStrategy(Wait.ForUnixContainer().UntilHttpRequestIsSucceeded(r => r
                        .ForPort(WebPort)
                        .ForPath("/health")))
                    .Build();
                await container.StartAsync().ConfigureAwait(false);
                _container = container;
            }

            await SeedAsync(_container).ConfigureAwait(false);
            return Url();
        }
        finally
        {
            Gate.Release();
        }
    }

    /// <summary>Takes an account out of a group (the directory's admin's change, while a session goes on).</summary>
    public static async Task RemoveFromGroupAsync(string uid, string group)
    {
        await Gate.WaitAsync().ConfigureAwait(false);
        try
        {
            using HttpClient api = await ApiAsync(_container!).ConfigureAwait(false);
            int id = (await GroupsAsync(api).ConfigureAwait(false))[group];
            await GraphQlAsync(
                api,
                "mutation($u:String!,$g:Int!){ removeUserFromGroup(userId:$u, groupId:$g){ ok } }",
                new JsonObject { ["u"] = uid, ["g"] = id })
                .ConfigureAwait(false);
        }
        finally
        {
            Gate.Release();
        }
    }

    /// <summary>Whether this password logs the account in, as LDAP checks it.</summary>
    public static async Task<bool> PasswordIsAsync(string uid, string password)
    {
        using LdapConnection connection = new();
        await connection
            .ConnectAsync(_container!.Hostname, _container.GetMappedPublicPort(LdapPort))
            .ConfigureAwait(false);
        try
        {
            await connection.BindAsync($"uid={uid},ou=people,{BaseDn}", password).ConfigureAwait(false);
            return true;
        }
        catch (LdapException ex) when (ex.ResultCode == LdapException.InvalidCredentials)
        {
            return false;
        }
    }

    private static string Url() =>
        string.Create(
            CultureInfo.InvariantCulture,
            $"ldap://{_container!.Hostname}:{_container.GetMappedPublicPort(LdapPort)}");

    private static async Task SeedAsync(IContainer container)
    {
        using HttpClient api = await ApiAsync(container).ConfigureAwait(false);
        JsonNode users = await GraphQlAsync(api, "{ users { id } }", []).ConfigureAwait(false);
        HashSet<string> existing = [.. users["users"]!.AsArray().Select(u => u!["id"]!.GetValue<string>())];
        Dictionary<string, int> groups = await GroupsAsync(api).ConfigureAwait(false);
        foreach (string group in new[] { UsersGroup, AdminsGroup }.Where(g => !groups.ContainsKey(g)))
        {
            JsonNode made = await GraphQlAsync(
                api,
                "mutation($n:String!){ createGroup(name:$n){ id } }",
                new JsonObject { ["n"] = group })
                .ConfigureAwait(false);
            groups[group] = made["createGroup"]!["id"]!.GetValue<int>();
        }

        foreach ((string uid, string name, string[] wanted) in People)
        {
            if (!existing.Contains(uid))
            {
                await GraphQlAsync(
                    api,
                    "mutation($u: CreateUserInput!){ createUser(user:$u){ id } }",
                    new JsonObject
                    {
                        ["u"] = new JsonObject
                        {
                            ["id"] = uid,
                            ["email"] = uid + "@example.com",
                            ["displayName"] = name,
                        },
                    })
                    .ConfigureAwait(false);
            }

            JsonNode user = await GraphQlAsync(
                api,
                "query($u:String!){ user(userId:$u){ groups { displayName } } }",
                new JsonObject { ["u"] = uid })
                .ConfigureAwait(false);
            HashSet<string> has =
                [.. user["user"]!["groups"]!.AsArray().Select(g => g!["displayName"]!.GetValue<string>())];
            foreach (string group in wanted.Except(has))
            {
                await MembershipAsync(api, "addUserToGroup", uid, groups[group]).ConfigureAwait(false);
            }

            foreach (string group in has.Except(wanted))
            {
                await MembershipAsync(api, "removeUserFromGroup", uid, groups[group]).ConfigureAwait(false);
            }
        }

        await SetPasswordsAsync(container).ConfigureAwait(false);
    }

    /// <summary>Every account's password back to its name's, as LLDAP's admin (the Password Modify operation).</summary>
    private static async Task SetPasswordsAsync(IContainer container)
    {
        using LdapConnection connection = new();
        await connection
            .ConnectAsync(container.Hostname, container.GetMappedPublicPort(LdapPort))
            .ConfigureAwait(false);
        await connection.BindAsync($"uid=admin,ou=people,{BaseDn}", AdminPassword).ConfigureAwait(false);
        foreach ((string uid, _, _) in People)
        {
            LdapExtendedResponse response = await connection
                .ExtendedOperationAsync(new LdapExtendedOperation(
                    NovellLdapDirectory.PasswordModifyOid,
                    NovellLdapDirectory.PasswordModifyRequest($"uid={uid},ou=people,{BaseDn}", null, PasswordOf(uid))))
                .ConfigureAwait(false);
            if (response.ResultCode != LdapException.Success)
            {
                throw new InvalidOperationException($"LLDAP didn't set {uid}'s password: {response.ErrorMessage}");
            }
        }
    }

    private static Task<JsonNode> MembershipAsync(HttpClient api, string mutation, string uid, int group) =>
        GraphQlAsync(
            api,
            $"mutation($u:String!,$g:Int!){{ {mutation}(userId:$u, groupId:$g){{ ok }} }}",
            new JsonObject { ["u"] = uid, ["g"] = group });

    private static async Task<Dictionary<string, int>> GroupsAsync(HttpClient api)
    {
        JsonNode groups = await GraphQlAsync(api, "{ groups { id displayName } }", []).ConfigureAwait(false);
        return groups["groups"]!
            .AsArray()
            .ToDictionary(g => g!["displayName"]!.GetValue<string>(), g => g!["id"]!.GetValue<int>());
    }

    private static async Task<HttpClient> ApiAsync(IContainer container)
    {
        HttpClient api = new()
        {
            BaseAddress = new Uri(string.Create(
                CultureInfo.InvariantCulture,
                $"http://{container.Hostname}:{container.GetMappedPublicPort(WebPort)}")),
        };
        using HttpResponseMessage login = await api
            .PostAsJsonAsync(
                new Uri("/auth/simple/login", UriKind.Relative),
                new { username = "admin", password = AdminPassword })
            .ConfigureAwait(false);
        login.EnsureSuccessStatusCode();
        JsonNode token = (await login.Content.ReadFromJsonAsync<JsonNode>().ConfigureAwait(false))!;
        string bearer = token["token"]!.GetValue<string>();
        api.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", bearer);
        return api;
    }

    private static async Task<JsonNode> GraphQlAsync(HttpClient api, string query, JsonObject variables)
    {
        using HttpResponseMessage response = await api
            .PostAsJsonAsync(
                new Uri("/api/graphql", UriKind.Relative),
                new JsonObject
                {
                    ["query"] = query,
                    ["variables"] = variables,
                })
            .ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        JsonNode body = (await response.Content.ReadFromJsonAsync<JsonNode>().ConfigureAwait(false))!;
        return body["errors"] is { } errors
            ? throw new InvalidOperationException($"LLDAP's GraphQL refused {query}: {errors.ToJsonString()}")
            : body["data"]!;
    }
}
