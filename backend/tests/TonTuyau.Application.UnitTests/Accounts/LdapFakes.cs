using TonTuyau.Application.Accounts;

namespace TonTuyau.Application.UnitTests.Accounts;

/// <summary>The LDAP settings saved, in memory: off unless a test turns them on (<see cref="On"/>).</summary>
internal sealed class FakeLdapSettings : ILdapSettingsRepository
{
    public const string Users = "(memberof=cn=tontuyau,ou=groups,dc=example,dc=com)";
    public const string Admins = "(memberof=cn=tontuyau_admin,ou=groups,dc=example,dc=com)";

    public LdapSettings Saved { get; set; } = LdapSettings.Default;

    /// <summary>On, as the user's LLDAP is set, the service account's password stored.</summary>
    public static LdapSettings On => LdapSettings.Default with
    {
        Enabled = true,
        Url = "ldap://ldap.example.com:3890",
        BindDn = "uid=tontuyau,ou=people,dc=example,dc=com",
        BindPassword = "service-password",
        BaseDn = "ou=people,dc=example,dc=com",
        UsersFilter = Users,
        AdminsFilter = Admins,
    };

    public Task<LdapSettings> GetAsync(CancellationToken cancellationToken) => Task.FromResult(Saved);

    public Task SaveAsync(LdapSettings settings, CancellationToken cancellationToken)
    {
        Saved = settings;
        return Task.CompletedTask;
    }
}

/// <summary>
/// A directory in memory: its people, their passwords, and the filters that find each one (a filter is matched as
/// written). <see cref="Down"/> makes every call fail as an unreachable directory does.
/// </summary>
internal sealed class FakeDirectory : ILdapDirectory
{
    private readonly Dictionary<string, (LdapPerson Person, HashSet<string> Filters)> _people =
        new(StringComparer.OrdinalIgnoreCase);

    public Dictionary<string, string> Passwords { get; } = new(StringComparer.Ordinal);

    public bool Down { get; set; }

    /// <summary>Why the next password change is refused; null: it's done.</summary>
    public string? RefuseChange { get; set; }

    public void Add(string uid, string displayName, string password, params string[] filters)
    {
        _people[uid] = (new LdapPerson($"uid={uid},ou=people,dc=example,dc=com", uid, displayName), [.. filters]);
        Passwords[$"uid={uid},ou=people,dc=example,dc=com"] = password;
    }

    public void Filters(string uid, params string[] filters) => _people[uid] = (_people[uid].Person, [.. filters]);

    public Task CheckAsync(LdapSettings settings, CancellationToken cancellationToken)
    {
        Reachable();
        return Task.CompletedTask;
    }

    public Task<LdapFound> FindAsync(LdapSettings settings, string filter, int max, CancellationToken cancellationToken)
    {
        Reachable();
        LdapPerson[] found = [.. _people.Values.Where(p => p.Filters.Contains(filter)).Select(p => p.Person)];
        return Task.FromResult(new LdapFound(found.Length, [.. found.Take(max)]));
    }

    public Task<LdapPerson?> FindUserAsync(
        LdapSettings settings,
        string username,
        string? filter,
        CancellationToken cancellationToken)
    {
        Reachable();
        return Task.FromResult(
            _people.TryGetValue(username, out (LdapPerson Person, HashSet<string> Filters) one)
                && (filter is null || one.Filters.Contains(filter))
                ? one.Person
                : null);
    }

    public Task<bool> CheckPasswordAsync(
        LdapSettings settings,
        string dn,
        string password,
        CancellationToken cancellationToken)
    {
        Reachable();
        return Task.FromResult(Passwords.TryGetValue(dn, out string? right) && right == password);
    }

    public Task ChangePasswordAsync(
        LdapSettings settings,
        string dn,
        string current,
        string password,
        CancellationToken cancellationToken)
    {
        Reachable();
        if (RefuseChange is not null)
        {
            throw new LdapRefusedException(RefuseChange);
        }

        Passwords[dn] = password;
        return Task.CompletedTask;
    }

    private void Reachable()
    {
        if (Down)
        {
            throw new LdapUnavailableException("ldap://ldap.example.com:3890: connection refused.");
        }
    }
}
