using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using TonTuyau.Application.Accounts;
using TonTuyau.Application.Validation;

namespace TonTuyau.Application.UnitTests.Accounts;

/// <summary>The LDAP page's rules (2.3.0): its settings, its tests, the names that clash.</summary>
public sealed class LdapServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 26, 9, 0, 0, TimeSpan.Zero);

    private readonly FakeLdapSettings _settings = new();
    private readonly FakeDirectory _directory = new();
    private readonly InMemoryAccounts _accounts = new();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    /// <summary>The user's LLDAP as the page sends it, the service account's password typed.</summary>
    private static LdapSettingsChange Typed(string? bindPassword = "service-password") => new(
        Enabled: true,
        Url: " ldap://ldap.example.com:3890 ",
        StartTls: false,
        CaCertificate: null,
        BindDn: "uid=tontuyau,ou=people,dc=example,dc=com",
        BindPassword: bindPassword,
        BaseDn: "ou=people,dc=example,dc=com",
        UidAttribute: "uid",
        UsernameAttribute: "cn",
        UsersFilter: FakeLdapSettings.Users,
        AdminsFilter: FakeLdapSettings.Admins,
        PasswordAttribute: "userPassword",
        PasswordResetUrl: "http://ldap.example.com:17170/$username/password");

    [Fact]
    public async Task Saved_trimmed_with_who_and_when_the_service_accounts_password_kept_when_none_is_typed()
    {
        LdapSettings first = await Service().SaveAsync(Typed(), "admin", Ct);
        LdapSettings second = await Service().SaveAsync(Typed(bindPassword: null) with { StartTls = true }, "boss", Ct);

        Assert.Equal("ldap://ldap.example.com:3890", first.Url);
        Assert.Equal(("admin", Now), (first.ChangedBy, first.ChangedAt));
        Assert.Equal("service-password", second.BindPassword);
        Assert.True(second.StartTls);
        Assert.Equal(second, _settings.Saved);
    }

    [Fact]
    public async Task On_each_field_missing_or_wrong_is_named_off_anything_goes()
    {
        LdapSettingsChange wrong = Typed(bindPassword: null) with
        {
            Url = "http://ldap.example.com",
            BaseDn = "",
            UsersFilter = "memberof=cn=tontuyau,ou=groups,dc=example,dc=com",
            PasswordResetUrl = "ldap.example.com/reset",
            CaCertificate = "not a certificate",
        };

        ValidationException error = await Assert.ThrowsAsync<ValidationException>(
            () => Service().SaveAsync(wrong, "admin", Ct));
        LdapSettings off = await Service().SaveAsync(wrong with { Enabled = false }, "admin", Ct);

        Assert.Equal(
            ["baseDn", "url", "bindPassword", "usersFilter", "passwordResetUrl", "caCertificate"],
            error.Errors.Select(e => e.Field));
        Assert.False(off.Enabled);
    }

    [Fact]
    public async Task Each_test_on_its_own_the_settings_as_typed_never_saved()
    {
        _directory.Add("paul", "Paul Martin", "paul-password", FakeLdapSettings.Users, FakeLdapSettings.Admins);
        _directory.Add("marie", "Marie Dubois", "marie-password", FakeLdapSettings.Users);
        _directory.Add("camille", "Camille Leroy", "camille-password", FakeLdapSettings.Users);
        _accounts.Add("camille");

        LdapTestResult connection = await Service().TestAsync(Typed(), LdapTestKind.Connection, null, Ct);
        LdapTestResult admins = await Service().TestAsync(Typed(), LdapTestKind.Admins, null, Ct);
        LdapTestResult users = await Service().TestAsync(Typed(), LdapTestKind.Users, null, Ct);

        Assert.True(connection.Connected);
        Assert.Equal(["paul"], admins.Found!.Select(f => f.Person.Username));
        Assert.Equal(3, users.FoundTotal);
        Assert.True(users.Found!.Single(f => f.Person.Username == "camille").Clash);
        Assert.Equal(LdapSettings.Default, _settings.Saved);
    }

    [Theory]
    [InlineData("paul", AccountRole.Admin, null)]
    [InlineData("marie", AccountRole.User, null)]
    [InlineData("zoe", null, "neither the users filter nor the admins filter finds it.")]
    [InlineData("nobody", null, "no such name under the base DN.")]
    [InlineData("camille", null, "a local account has that name.")]
    public async Task A_name_looked_up_says_the_role_it_would_get_or_why_its_refused(
        string username,
        AccountRole? role,
        string? refused)
    {
        _directory.Add("paul", "Paul Martin", "x", FakeLdapSettings.Users, FakeLdapSettings.Admins);
        _directory.Add("marie", "Marie Dubois", "x", FakeLdapSettings.Users);
        _directory.Add("zoe", "Zoé Bernard", "x");
        _directory.Add("camille", "Camille Leroy", "x", FakeLdapSettings.Users);
        _accounts.Add("camille");

        LdapLookup lookup = (await Service().TestAsync(Typed(), LdapTestKind.Name, username, Ct)).Lookup!;

        Assert.Equal((role, refused), (lookup.Role, lookup.Refused));
    }

    [Fact]
    public async Task A_directory_not_answering_is_a_test_not_connected_saying_why()
    {
        _directory.Down = true;

        LdapTestResult result = await Service().TestAsync(Typed(), LdapTestKind.Users, null, Ct);

        Assert.False(result.Connected);
        Assert.Contains("connection refused", result.Error, StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_local_names_the_directory_has_too_clash()
    {
        _directory.Add("camille", "Camille Leroy", "x", FakeLdapSettings.Users);
        _accounts.Add("camille");
        _accounts.Add("admin");

        Assert.Equal(["camille"], await Service().ClashesAsync(FakeLdapSettings.On, Ct));
        Assert.Empty(await Service().ClashesAsync(FakeLdapSettings.On with { Enabled = false }, Ct));
        _directory.Down = true;
        Assert.Empty(await Service().ClashesAsync(FakeLdapSettings.On, Ct));
    }

    private LdapService Service() =>
        new(_settings, _directory, _accounts, new FakeTimeProvider(Now), NullLogger<LdapService>.Instance);

    /// <summary>Local accounts by name only: what the LDAP rules ask of them.</summary>
    private sealed class InMemoryAccounts : IAccountRepository
    {
        private readonly List<Account> _rows = [];

        public void Add(string username) =>
            _rows.Add(new Account(
                _rows.Count + 1,
                username,
                DisplayName: null,
                AccountRole.User,
                AccountSource.Local,
                Disabled: false,
                "hash",
                "stamp",
                Now,
                LastLoginAt: null));

        public Task<bool> AnyAsync(CancellationToken cancellationToken) => Task.FromResult(_rows.Count > 0);

        public Task<IReadOnlyList<Account>> ListAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<Account>>([.. _rows]);

        public Task<Account?> FindAsync(int id, CancellationToken cancellationToken) =>
            Task.FromResult(_rows.SingleOrDefault(a => a.Id == id));

        public Task<Account?> FindByUsernameAsync(string username, CancellationToken cancellationToken) =>
            Task.FromResult(_rows.SingleOrDefault(a => a.Username == username));

        public Task<bool> NameTakenAsync(string username, CancellationToken cancellationToken) =>
            Task.FromResult(_rows.Any(a => string.Equals(a.Username, username, StringComparison.OrdinalIgnoreCase)));

        public Task<Account> AddAsync(Account account, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task UpdateAsync(Account account, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task DeleteAsync(int id, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task SetLastLoginAsync(int id, DateTimeOffset at, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }
}
