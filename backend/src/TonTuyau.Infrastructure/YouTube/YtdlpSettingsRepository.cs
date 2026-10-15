using Microsoft.EntityFrameworkCore;
using TonTuyau.Application.YouTube;
using TonTuyau.Infrastructure.Accounts;

namespace TonTuyau.Infrastructure.YouTube;

/// <summary>The yt-dlp choice's database calls (EF Core, PostgreSQL). The rules are in <see cref="YtdlpService"/>.</summary>
internal sealed class YtdlpSettingsRepository(TonTuyauDbContext db) : IYtdlpSettingsRepository
{
    public async Task<YtdlpSettings> GetAsync(CancellationToken cancellationToken)
    {
        YtdlpSettingsRow? row = await db.YtdlpSettings
            .AsNoTracking()
            .SingleOrDefaultAsync(s => s.Id == YtdlpSettingsRow.OnlyId, cancellationToken)
            .ConfigureAwait(false);
        return row is null ? new YtdlpSettings(YtdlpChoice.Default, null, null) : (YtdlpSettings)row;
    }

    public async Task SaveChoiceAsync(YtdlpChoice choice, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(choice);
        YtdlpSettingsRow row = await RowAsync(cancellationToken).ConfigureAwait(false);
        row.Kind = choice.Kind;
        row.Version = choice.Version;
        row.ChosenBy = choice.ChosenBy;
        row.ChosenAt = choice.ChosenAt;
        await db
            .SaveChangesAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task SaveCheckAsync(DateTimeOffset checkedAt, string? failure, CancellationToken cancellationToken)
    {
        YtdlpSettingsRow row = await RowAsync(cancellationToken).ConfigureAwait(false);
        row.CheckedAt = checkedAt;
        row.Error = failure;
        await db
            .SaveChangesAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>The row, tracked; a new one (the default choice) when there's none yet.</summary>
    private async Task<YtdlpSettingsRow> RowAsync(CancellationToken cancellationToken)
    {
        YtdlpSettingsRow? row = await db.YtdlpSettings
            .SingleOrDefaultAsync(s => s.Id == YtdlpSettingsRow.OnlyId, cancellationToken)
            .ConfigureAwait(false);
        if (row is null)
        {
            row = new YtdlpSettingsRow { Kind = YtdlpChoiceKind.Latest };
            db.YtdlpSettings.Add(row);
        }

        return row;
    }
}
