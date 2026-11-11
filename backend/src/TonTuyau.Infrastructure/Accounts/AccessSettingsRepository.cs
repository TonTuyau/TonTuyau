using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TonTuyau.Application.Accounts;

namespace TonTuyau.Infrastructure.Accounts;

/// <summary>
/// Who can watch's database calls (2.3.0): a singleton (read for the requests to the library), a database context per
/// call.
/// </summary>
internal sealed class AccessSettingsRepository(IServiceScopeFactory scopes) : IAccessSettingsRepository
{
    public async Task<AccessSettings> GetAsync(CancellationToken cancellationToken)
    {
        AsyncServiceScope scope = scopes.CreateAsyncScope();
        await using (scope.ConfigureAwait(false))
        {
            AccessSettingsRow? row = await scope.ServiceProvider
                .GetRequiredService<TonTuyauDbContext>()
                .AccessSettings
                .AsNoTracking()
                .SingleOrDefaultAsync(s => s.Id == AccessSettingsRow.OnlyId, cancellationToken)
                .ConfigureAwait(false);
            return row is null ? AccessSettings.Default : (AccessSettings)row;
        }
    }

    public async Task SaveAsync(AccessSettings settings, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(settings);
        AsyncServiceScope scope = scopes.CreateAsyncScope();
        await using (scope.ConfigureAwait(false))
        {
            TonTuyauDbContext db = scope.ServiceProvider.GetRequiredService<TonTuyauDbContext>();
            AccessSettingsRow? row = await db.AccessSettings
                .SingleOrDefaultAsync(s => s.Id == AccessSettingsRow.OnlyId, cancellationToken)
                .ConfigureAwait(false);
            if (row is null)
            {
                row = new AccessSettingsRow();
                db.AccessSettings.Add(row);
            }

            row.AccountsOnly = settings.AccountsOnly;
            row.ChangedBy = settings.ChangedBy;
            row.ChangedAt = settings.ChangedAt;
            await db
                .SaveChangesAsync(cancellationToken)
                .ConfigureAwait(false);
        }
    }
}
