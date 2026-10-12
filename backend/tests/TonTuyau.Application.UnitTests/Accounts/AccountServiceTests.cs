using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using TonTuyau.Application.Accounts;
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

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task The_admin_is_created_when_there_is_no_account()
    {
        await Service("s3cret").EnsureAdminAsync(Ct);

        Account admin = Assert.Single(_accounts.All);
        Assert.Equal("admin", admin.Username);
        Assert.Equal("hash:s3cret", admin.PasswordHash);
        Assert.Equal(Now, admin.CreatedAt);
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

    private AccountService Service(string adminPassword = "unused") => new(
        _accounts,
        _hasher,
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

        public void Add(string username, string hash) =>
            _rows.Add(new Account(_rows.Count + 1, username, hash, Now.AddDays(-1), null));

        public Task<bool> AnyAsync(CancellationToken cancellationToken) => Task.FromResult(_rows.Count > 0);

        public Task<Account?> FindByUsernameAsync(string username, CancellationToken cancellationToken)
        {
            Lookups++;
            return Task.FromResult(_rows.SingleOrDefault(a => a.Username == username));
        }

        public Task AddAsync(
            string username,
            string passwordHash,
            DateTimeOffset createdAt,
            CancellationToken cancellationToken)
        {
            _rows.Add(new Account(_rows.Count + 1, username, passwordHash, createdAt, null));
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
