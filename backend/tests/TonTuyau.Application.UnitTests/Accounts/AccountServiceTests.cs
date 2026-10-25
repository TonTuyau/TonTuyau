using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using TonTuyau.Application.Accounts;
using TonTuyau.Application.Browse;
using TonTuyau.Application.Configuration;
using TonTuyau.Application.UnitTests.Configuration;
using TonTuyau.Application.Validation;

namespace TonTuyau.Application.UnitTests.Accounts;

/// <summary>The accounts' rules, without a database: <see cref="IAccountRepository"/> and the hasher are fakes.</summary>
public sealed class AccountServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 12, 9, 0, 0, TimeSpan.Zero);

    private readonly FakeAccounts _accounts = new();
    private readonly FakeHasher _hasher = new();
    private readonly FakeLdapSettings _ldap = new();
    private readonly FakeDirectory _directory = new();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task The_admin_is_created_when_there_is_no_account()
    {
        await Service("s3cret").EnsureAdminAsync(Ct);

        Account admin = Assert.Single(_accounts.All);
        Assert.Equal("admin", admin.Username);
        Assert.Equal("hash:s3cret", admin.PasswordHash);
        Assert.Equal(Now, admin.CreatedAt);
        Assert.Equal((AccountRole.Admin, AccountSource.Local, false), (admin.Role, admin.Source, admin.Disabled));
        Assert.NotEmpty(admin.Stamp);
    }

    [Fact]
    public async Task No_admin_is_created_once_an_account_exists()
    {
        _accounts.Add("someone", "hash:x");

        await Service("s3cret").EnsureAdminAsync(Ct);

        Assert.Equal(["someone"], _accounts.All.Select(a => a.Username));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Without_a_password_and_no_account_the_start_stops(string password)
    {
        ValidationException error = await Assert.ThrowsAsync<ValidationException>(
            () => Service(password).EnsureAdminAsync(Ct));

        Assert.Contains(error.Errors, e => e.Field == ConfigKeys.EnvironmentName(ConfigKeys.AdminPassword));
        Assert.Empty(_accounts.All);
    }

    [Fact]
    public async Task The_right_password_opens_the_account_and_sets_its_last_login()
    {
        _accounts.Add("admin", "hash:s3cret");

        Account? account = await Service().LoginAsync("admin", "s3cret", Ct);

        Assert.NotNull(account);
        Assert.Equal(Now, account.LastLoginAt);
        Assert.Equal(Now, _accounts.All.Single().LastLoginAt);
    }

    [Theory]
    [InlineData("admin", "wrong")]
    [InlineData("nobody", "s3cret")]
    [InlineData("Admin", "s3cret")]
    [InlineData("", "s3cret")]
    [InlineData(null, "s3cret")]
    [InlineData("admin", "")]
    [InlineData("admin", null)]
    public async Task Any_refusal_is_null_after_exactly_one_password_check(string? username, string? password)
    {
        _accounts.Add("admin", "hash:s3cret");

        Account? account = await Service().LoginAsync(username, password, Ct);

        Assert.Null(account);
        // One check whatever the reason (the hasher's first run made the unknown-name hash): timing says nothing.
        Assert.Equal(1, _hasher.Verifications);
        Assert.Null(_accounts.All.Single().LastLoginAt);
    }

    [Fact]
    public async Task A_name_or_password_too_long_is_refused_without_asking_the_database()
    {
        _accounts.Add(new string('a', AccountService.MaxUsernameLength + 1), "hash:s3cret");

        Assert.Null(await Service().LoginAsync(new string('a', AccountService.MaxUsernameLength + 1), "s3cret", Ct));
        Assert.Null(await Service().LoginAsync("admin", new string('p', AccountService.MaxPasswordLength + 1), Ct));
        Assert.Equal(0, _accounts.Lookups);
    }

    [Fact]
    public async Task A_disabled_account_or_a_directory_one_with_the_directory_off_is_refused_after_one_check()
    {
        Account camille = _accounts.Add("camille", "hash:s3cret") with { Disabled = true };
        _accounts.Replace(camille);
        _accounts.Replace(_accounts.Add("marie", "hash:s3cret") with { Source = AccountSource.Ldap });

        Assert.Null(await Service().LoginAsync("camille", "s3cret", Ct));
        Assert.Null(await Service().LoginAsync("marie", "s3cret", Ct));
        Assert.Equal(2, _hasher.Verifications);
    }

    // ---------------------------------------------------------------- 2.3.0: the accounts page

    [Fact]
    public async Task A_local_account_is_made_enabled_with_its_role_and_its_password_hashed()
    {
        Account made = await Service().CreateAsync("  Léa ", "un long mot", "user", "admin", Ct);

        Assert.Equal(("Léa", AccountRole.User, AccountSource.Local), (made.Username, made.Role, made.Source));
        Assert.Equal("hash:un long mot", made.PasswordHash);
        Assert.False(made.Disabled);
        Assert.Null(made.DisplayName);
    }

    [Theory]
    [InlineData(null, "un long mot", "user", "username")]
    [InlineData("léa", "court", "user", "password")]
    [InlineData("léa", null, "user", "password")]
    [InlineData("léa", "un long mot", null, "role")]
    [InlineData("léa", "un long mot", "owner", "role")]
    [InlineData("lé\ta", "un long mot", "user", "username")]
    public async Task A_new_account_without_what_it_needs_is_refused(
        string? username,
        string? password,
        string? role,
        string field)
    {
        ValidationException error = await Assert.ThrowsAsync<ValidationException>(
            () => Service().CreateAsync(username, password, role, "admin", Ct));

        Assert.Equal([field], error.Errors.Select(e => e.Field));
        Assert.Empty(_accounts.All);
    }

    [Fact]
    public async Task A_name_taken_whatever_the_case_is_a_conflict()
    {
        _accounts.Add("Camille", "hash:x");

        await Assert.ThrowsAsync<ConflictException>(
            () => Service().CreateAsync("camille", "un long mot", "user", "admin", Ct));
    }

    [Fact]
    public async Task A_change_gives_a_new_stamp_the_sessions_before_end()
    {
        Account admin = Admin();
        Account camille = _accounts.Add("camille", "hash:x");

        Account changed = await Service().ChangeAsync(camille.Id, "admin", "nouveau mot", null, admin.Id, "admin", Ct);

        Assert.Equal(AccountRole.Admin, changed.Role);
        Assert.Equal("hash:nouveau mot", changed.PasswordHash);
        Assert.NotEqual(camille.Stamp, changed.Stamp);
        Assert.Null(await Service().SessionAsync(camille.Id, camille.Stamp, Ct));
        Assert.NotNull(await Service().SessionAsync(camille.Id, changed.Stamp, Ct));
    }

    [Fact]
    public async Task Nothing_changed_keeps_the_stamp()
    {
        Account admin = Admin();
        Account camille = _accounts.Add("camille", "hash:x");

        Account same = await Service().ChangeAsync(camille.Id, "user", null, false, admin.Id, "admin", Ct);

        Assert.Equal(camille.Stamp, same.Stamp);
    }

    [Fact]
    public async Task The_last_enabled_local_admin_stays_a_directory_admin_doesnt_count()
    {
        Account admin = Admin();
        _accounts.Replace(_accounts.Add("paul", null, AccountRole.Admin) with { Source = AccountSource.Ldap });
        Account other = _accounts.Add("other", "hash:x");

        await Assert.ThrowsAsync<ConflictException>(
            () => Service().ChangeAsync(admin.Id, "user", null, null, other.Id, "other", Ct));
        await Assert.ThrowsAsync<ConflictException>(
            () => Service().ChangeAsync(admin.Id, null, null, true, other.Id, "other", Ct));
        await Assert.ThrowsAsync<ConflictException>(() => Service().DeleteAsync(admin.Id, other.Id, "other", Ct));

        // A second local admin: the first can go.
        await Service().ChangeAsync(other.Id, "admin", null, null, admin.Id, "admin", Ct);
        await Service().DeleteAsync(admin.Id, other.Id, "other", Ct);
        Assert.DoesNotContain(_accounts.All, a => a.Id == admin.Id);
    }

    [Fact]
    public async Task Nobody_disables_nor_deletes_their_own_account()
    {
        Account admin = Admin();
        _accounts.Add("second", "hash:x", AccountRole.Admin);

        await Assert.ThrowsAsync<ConflictException>(
            () => Service().ChangeAsync(admin.Id, null, null, true, admin.Id, "admin", Ct));
        await Assert.ThrowsAsync<ConflictException>(() => Service().DeleteAsync(admin.Id, admin.Id, "admin", Ct));
    }

    [Fact]
    public async Task A_directory_accounts_role_and_password_are_the_directorys_it_can_be_disabled()
    {
        Account admin = Admin();
        Account marie = _accounts.Add("marie", null) with { Source = AccountSource.Ldap };
        _accounts.Replace(marie);

        await Assert.ThrowsAsync<ConflictException>(
            () => Service().ChangeAsync(marie.Id, "admin", null, null, admin.Id, "admin", Ct));
        await Assert.ThrowsAsync<ConflictException>(
            () => Service().ChangeAsync(marie.Id, null, "un long mot", null, admin.Id, "admin", Ct));
        Assert.True((await Service().ChangeAsync(marie.Id, null, null, true, admin.Id, "admin", Ct)).Disabled);
    }

    [Fact]
    public async Task An_unknown_account_is_not_found()
    {
        await Assert.ThrowsAsync<NotFoundException>(() => Service().ChangeAsync(9, null, null, true, 1, "admin", Ct));
        await Assert.ThrowsAsync<NotFoundException>(() => Service().DeleteAsync(9, 1, "admin", Ct));
    }

    // ---------------------------------------------------------------- 2.3.0: one's own password, the sessions

    [Fact]
    public async Task One_changes_their_password_with_the_current_one_and_gets_a_new_stamp()
    {
        Account camille = _accounts.Add("camille", "hash:ancien mot");

        ValidationException wrong = await Assert.ThrowsAsync<ValidationException>(
            () => Service().ChangeOwnPasswordAsync(camille.Id, "pas le bon", "nouveau mot", Ct));
        Account changed = await Service().ChangeOwnPasswordAsync(camille.Id, "ancien mot", "nouveau mot", Ct);

        Assert.Equal(["current"], wrong.Errors.Select(e => e.Field));
        Assert.Equal("hash:nouveau mot", changed.PasswordHash);
        Assert.NotEqual(camille.Stamp, changed.Stamp);
    }

    [Fact]
    public async Task A_directory_account_doesnt_change_its_password_while_the_directory_is_off()
    {
        Account marie = _accounts.Add("marie", null) with { Source = AccountSource.Ldap };
        _accounts.Replace(marie);

        await Assert.ThrowsAsync<ConflictException>(
            () => Service().ChangeOwnPasswordAsync(marie.Id, "x", "nouveau mot", Ct));
    }

    [Fact]
    public async Task A_session_goes_on_while_its_account_is_there_enabled_with_the_same_stamp()
    {
        Account camille = _accounts.Add("camille", "hash:x");

        Assert.NotNull(await Service().SessionAsync(camille.Id, camille.Stamp, Ct));
        Assert.Null(await Service().SessionAsync(camille.Id, null, Ct));
        Assert.Null(await Service().SessionAsync(camille.Id, "another", Ct));
        Assert.Null(await Service().SessionAsync(99, camille.Stamp, Ct));
        _accounts.Replace(camille with { Disabled = true });
        Assert.Null(await Service().SessionAsync(camille.Id, camille.Stamp, Ct));
    }

    private Account Admin() => _accounts.Add("admin", "hash:s3cret", AccountRole.Admin);

    // ---------------------------------------------------------------- 2.3.0: the LDAP directory

    /// <summary>The directory on: marie a user, paul an admin (both filters), zoe in none.</summary>
    private void DirectoryOn()
    {
        _ldap.Saved = FakeLdapSettings.On;
        _directory.Add("marie", "Marie Dubois", "marie-password", FakeLdapSettings.Users);
        _directory.Add("paul", "Paul Martin", "paul-password", FakeLdapSettings.Users, FakeLdapSettings.Admins);
        _directory.Add("zoe", "Zoé Bernard", "zoe-password");
    }

    [Fact]
    public async Task Off_the_directory_isnt_asked()
    {
        _directory.Add("marie", "Marie Dubois", "marie-password", FakeLdapSettings.Users);

        Assert.Null(await Service().LoginAsync("marie", "marie-password", Ct));
    }

    [Fact]
    public async Task A_directory_login_makes_its_row_with_its_name_and_the_role_its_filters_give()
    {
        DirectoryOn();

        Account? marie = await Service().LoginAsync("marie", "marie-password", Ct);
        Account? paul = await Service().LoginAsync("paul", "paul-password", Ct);

        Assert.NotNull(marie);
        Assert.Equal(
            ("Marie Dubois", AccountRole.User, AccountSource.Ldap),
            (marie.DisplayName, marie.Role, marie.Source));
        Assert.Null(marie.PasswordHash);
        Assert.Equal(Now, marie.LastLoginAt);
        Assert.Equal(AccountRole.Admin, paul?.Role);
        Assert.Equal(2, _accounts.All.Count(a => a.Source == AccountSource.Ldap));
    }

    [Theory]
    [InlineData("marie", "wrong")]
    [InlineData("zoe", "zoe-password")]
    [InlineData("nobody", "x")]
    public async Task A_wrong_password_or_an_account_no_filter_finds_is_refused(string username, string password)
    {
        DirectoryOn();

        Assert.Null(await Service().LoginAsync(username, password, Ct));
        Assert.DoesNotContain(_accounts.All, a => a.Source == AccountSource.Ldap);
    }

    [Fact]
    public async Task A_name_both_local_and_the_directorys_is_the_local_accounts_only()
    {
        DirectoryOn();
        _accounts.Add("marie", "hash:local-password");

        Assert.Null(await Service().LoginAsync("marie", "marie-password", Ct));
        Assert.Equal(AccountSource.Local, (await Service().LoginAsync("marie", "local-password", Ct))?.Source);
    }

    [Fact]
    public async Task A_disabled_directory_account_is_refused_and_a_down_directory_logs_nobody_in()
    {
        DirectoryOn();
        Account marie = (await Service().LoginAsync("marie", "marie-password", Ct))!;
        _accounts.Replace(marie with { Disabled = true });

        Assert.Null(await Service().LoginAsync("marie", "marie-password", Ct));
        _directory.Down = true;
        Assert.Null(await Service().LoginAsync("paul", "paul-password", Ct));
    }

    [Fact]
    public async Task A_directory_session_follows_the_directory_its_role_or_its_filters_or_it_ends()
    {
        DirectoryOn();
        Account marie = (await Service().LoginAsync("marie", "marie-password", Ct))!;
        Assert.NotNull(await Service().SessionAsync(marie.Id, marie.Stamp, Ct));

        // Not answering: kept.
        _directory.Down = true;
        Assert.NotNull(await Service().SessionAsync(marie.Id, marie.Stamp, Ct));
        _directory.Down = false;

        // Made an admin: the row takes it, the session ends (logged in again, it's an admin).
        _directory.Filters("marie", FakeLdapSettings.Users, FakeLdapSettings.Admins);
        Assert.Null(await Service().SessionAsync(marie.Id, marie.Stamp, Ct));
        Account admin = _accounts.All.Single(a => a.Username == "marie");
        Assert.Equal(AccountRole.Admin, admin.Role);
        Assert.NotNull(await Service().SessionAsync(admin.Id, admin.Stamp, Ct));

        // Out of the groups, or the directory turned off: ended.
        _directory.Filters("marie");
        Assert.Null(await Service().SessionAsync(admin.Id, admin.Stamp, Ct));
        _directory.Filters("marie", FakeLdapSettings.Users, FakeLdapSettings.Admins);
        _ldap.Saved = _ldap.Saved with { Enabled = false };
        Assert.Null(await Service().SessionAsync(admin.Id, admin.Stamp, Ct));
    }

    [Fact]
    public async Task A_local_account_cant_take_a_name_the_directory_has_unless_it_cant_be_asked()
    {
        DirectoryOn();

        await Assert.ThrowsAsync<ConflictException>(
            () => Service().CreateAsync("Marie", "un long mot", "user", "admin", Ct));
        _directory.Down = true;
        Assert.Equal("marie", (await Service().CreateAsync("marie", "un long mot", "user", "admin", Ct)).Username);
    }

    [Fact]
    public async Task A_directory_account_changes_its_password_in_the_directory_its_current_one_first()
    {
        DirectoryOn();
        Account marie = (await Service().LoginAsync("marie", "marie-password", Ct))!;
        const string Dn = "uid=marie,ou=people,dc=example,dc=com";

        ValidationException wrong = await Assert.ThrowsAsync<ValidationException>(
            () => Service().ChangeOwnPasswordAsync(marie.Id, "nope", "nouveau mot", Ct));
        Account changed = await Service().ChangeOwnPasswordAsync(marie.Id, "marie-password", "nouveau mot", Ct);

        Assert.Equal(["current"], wrong.Errors.Select(e => e.Field));
        Assert.Equal("nouveau mot", _directory.Passwords[Dn]);
        Assert.NotEqual(marie.Stamp, changed.Stamp);
        Assert.Null(changed.PasswordHash);
    }

    [Fact]
    public async Task The_directory_refusing_the_new_password_says_why()
    {
        DirectoryOn();
        Account marie = (await Service().LoginAsync("marie", "marie-password", Ct))!;
        _directory.RefuseChange = "Insufficient Access Rights";

        ConflictException refused = await Assert.ThrowsAsync<ConflictException>(
            () => Service().ChangeOwnPasswordAsync(marie.Id, "marie-password", "nouveau mot", Ct));

        Assert.Contains("Insufficient Access Rights", refused.Message, StringComparison.Ordinal);
    }

    private AccountService Service(string adminPassword = "unused") => new(
        _accounts,
        _hasher,
        new LdapService(
            _ldap,
            _directory,
            _accounts,
            new FakeTimeProvider(Now),
            NullLogger<LdapService>.Instance),
        _directory,
        ValidOptions.Create() with { Accounts = new AccountOptions("admin", adminPassword, CookieSecure: true) },
        new FakeTimeProvider(Now),
        NullLogger<AccountService>.Instance);

    /// <summary>"hash:" + the password: enough to tell right from wrong, and to count the checks.</summary>
    private sealed class FakeHasher : IPasswordHasher
    {
        public int Verifications { get; private set; }

        public string Hash(string password) => "hash:" + password;

        public bool Verify(string password, string hash)
        {
            Verifications++;
            return hash == "hash:" + password;
        }
    }

    private sealed class FakeAccounts : IAccountRepository
    {
        private readonly List<Account> _rows = [];

        public IReadOnlyList<Account> All => _rows;

        public int Lookups { get; private set; }

        /// <summary>A local account, enabled (a user unless said), with a stamp of its own.</summary>
        public Account Add(string username, string? hash, AccountRole role = AccountRole.User)
        {
            Account account = new(
                _rows.Count == 0 ? 1 : _rows.Max(a => a.Id) + 1,
                username,
                DisplayName: null,
                role,
                AccountSource.Local,
                Disabled: false,
                hash,
                "stamp-" + username,
                Now.AddDays(-1),
                LastLoginAt: null);
            _rows.Add(account);
            return account;
        }

        public void Replace(Account account) => _rows[_rows.FindIndex(a => a.Id == account.Id)] = account;

        public Task<bool> AnyAsync(CancellationToken cancellationToken) => Task.FromResult(_rows.Count > 0);

        public Task<IReadOnlyList<Account>> ListAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<Account>>([.. _rows]);

        public Task<Account?> FindAsync(int id, CancellationToken cancellationToken) =>
            Task.FromResult(_rows.SingleOrDefault(a => a.Id == id));

        public Task<Account?> FindByUsernameAsync(string username, CancellationToken cancellationToken)
        {
            Lookups++;
            return Task.FromResult(_rows.SingleOrDefault(a => a.Username == username));
        }

        public Task<bool> NameTakenAsync(string username, CancellationToken cancellationToken) =>
            Task.FromResult(_rows.Any(a => string.Equals(a.Username, username, StringComparison.OrdinalIgnoreCase)));

        public Task<Account> AddAsync(Account account, CancellationToken cancellationToken)
        {
            Account added = account with { Id = _rows.Count + 1 };
            _rows.Add(added);
            return Task.FromResult(added);
        }

        public Task UpdateAsync(Account account, CancellationToken cancellationToken)
        {
            Replace(account);
            return Task.CompletedTask;
        }

        public Task DeleteAsync(int id, CancellationToken cancellationToken)
        {
            _rows.RemoveAll(a => a.Id == id);
            return Task.CompletedTask;
        }

        public Task SetLastLoginAsync(int id, DateTimeOffset at, CancellationToken cancellationToken)
        {
            int index = _rows.FindIndex(a => a.Id == id);
            _rows[index] = _rows[index] with { LastLoginAt = at };
            return Task.CompletedTask;
        }
    }
}
