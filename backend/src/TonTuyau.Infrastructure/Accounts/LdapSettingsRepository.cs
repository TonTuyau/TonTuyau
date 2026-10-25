using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TonTuyau.Application.Accounts;

namespace TonTuyau.Infrastructure.Accounts;

/// <summary>The LDAP settings' database calls (2.3.0): a singleton, a database context per call.</summary>
internal sealed class LdapSettingsRepository(IServiceScopeFactory scopes) : ILdapSettingsRepository
{
    public async Task<LdapSettings> GetAsync(CancellationToken cancellationToken)
    {
        AsyncServiceScope scope = scopes.CreateAsyncScope();
        await using (scope.ConfigureAwait(false))
        {
            LdapSettingsRow? row = await scope.ServiceProvider
                .GetRequiredService<TonTuyauDbContext>()
                .LdapSettings
                .AsNoTracking()
                .SingleOrDefaultAsync(s => s.Id == LdapSettingsRow.OnlyId, cancellationToken)
                .ConfigureAwait(false);
            return row is null ? LdapSettings.Default : (LdapSettings)row;
        }
    }

    public async Task SaveAsync(LdapSettings settings, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(settings);
        AsyncServiceScope scope = scopes.CreateAsyncScope();
        await using (scope.ConfigureAwait(false))
        {
            TonTuyauDbContext db = scope.ServiceProvider.GetRequiredService<TonTuyauDbContext>();
            LdapSettingsRow? row = await db.LdapSettings
                .SingleOrDefaultAsync(s => s.Id == LdapSettingsRow.OnlyId, cancellationToken)
                .ConfigureAwait(false);
            if (row is null)
            {
                row = new LdapSettingsRow
                {
                    Url = "",
                    BindDn = "",
                    BaseDn = "",
                    UidAttribute = "",
                    UsernameAttribute = "",
                    UsersFilter = "",
                    AdminsFilter = "",
                    PasswordAttribute = "",
                };
                db.LdapSettings.Add(row);
            }

            row.Enabled = settings.Enabled;
            row.Url = settings.Url;
            row.StartTls = settings.StartTls;
            row.CaCertificate = settings.CaCertificate;
            row.BindDn = settings.BindDn;
            row.BindPassword = settings.BindPassword;
            row.BaseDn = settings.BaseDn;
            row.UidAttribute = settings.UidAttribute;
            row.UsernameAttribute = settings.UsernameAttribute;
            row.UsersFilter = settings.UsersFilter;
            row.AdminsFilter = settings.AdminsFilter;
            row.PasswordAttribute = settings.PasswordAttribute;
            row.PasswordResetUrl = settings.PasswordResetUrl;
            row.ChangedBy = settings.ChangedBy;
            row.ChangedAt = settings.ChangedAt;
            await db
                .SaveChangesAsync(cancellationToken)
                .ConfigureAwait(false);
        }
    }
}
