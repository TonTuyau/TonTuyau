using Microsoft.EntityFrameworkCore;
using TonTuyau.Application.YouTube;
using TonTuyau.Infrastructure.Accounts;

namespace TonTuyau.Infrastructure.YouTube;

/// <summary>The YouTube cookie's database calls (EF Core, PostgreSQL). The rules are in <see cref="YouTubeCookieService"/>.</summary>
internal sealed class YouTubeCookieRepository(TonTuyauDbContext db) : IYouTubeCookieRepository
{
    public async Task<YouTubeCookie?> GetAsync(CancellationToken cancellationToken)
    {
        YouTubeCookieRow? row = await db.YouTubeCookie
            .AsNoTracking()
            .SingleOrDefaultAsync(c => c.Id == YouTubeCookieRow.OnlyId, cancellationToken)
            .ConfigureAwait(false);
        return row is null ? null : (YouTubeCookie)row;
    }

    public async Task SaveAsync(
        string content,
        DateTimeOffset receivedAt,
        string receivedBy,
        CancellationToken cancellationToken)
    {
        YouTubeCookieRow? row = await db.YouTubeCookie
            .SingleOrDefaultAsync(c => c.Id == YouTubeCookieRow.OnlyId, cancellationToken)
            .ConfigureAwait(false);
        if (row is null)
        {
            row = new YouTubeCookieRow { Content = content, ReceivedBy = receivedBy };
            db.YouTubeCookie.Add(row);
        }

        row.Content = content;
        row.ReceivedAt = receivedAt;
        row.ReceivedBy = receivedBy;
        row.State = YouTubeCookieState.Unchecked;
        row.CheckedAt = null;
        row.Reason = null;
        await db
            .SaveChangesAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public Task SetCheckAsync(
        YouTubeCookieState state,
        DateTimeOffset checkedAt,
        string? reason,
        CancellationToken cancellationToken) =>
        db.YouTubeCookie
            .Where(c => c.Id == YouTubeCookieRow.OnlyId)
            .ExecuteUpdateAsync(
                set => set
                    .SetProperty(c => c.State, state)
                    .SetProperty(c => c.CheckedAt, checkedAt)
                    .SetProperty(c => c.Reason, reason),
                cancellationToken);

    public async Task<bool> DeleteAsync(CancellationToken cancellationToken) =>
        await db.YouTubeCookie
            .Where(c => c.Id == YouTubeCookieRow.OnlyId)
            .ExecuteDeleteAsync(cancellationToken)
            .ConfigureAwait(false) > 0;
}
