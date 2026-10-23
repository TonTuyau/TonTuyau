using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TonTuyau.Application.YouTube;
using TonTuyau.Infrastructure.Accounts;

namespace TonTuyau.Infrastructure.YouTube;

/// <summary>
/// The YouTube settings' database calls: a singleton (the refreshes read it at each run), a database context per call.
/// </summary>
internal sealed class YouTubeSettingsRepository(IServiceScopeFactory scopes) : IYouTubeSettingsRepository
{
    public async Task<YouTubeSettings> GetAsync(CancellationToken cancellationToken)
    {
        AsyncServiceScope scope = scopes.CreateAsyncScope();
        await using (scope.ConfigureAwait(false))
        {
            YouTubeSettingsRow? row = await scope.ServiceProvider
                .GetRequiredService<TonTuyauDbContext>()
                .YouTubeSettings
                .AsNoTracking()
                .SingleOrDefaultAsync(s => s.Id == YouTubeSettingsRow.OnlyId, cancellationToken)
                .ConfigureAwait(false);
            return row is null ? YouTubeSettings.Default : (YouTubeSettings)row;
        }
    }

    public async Task SaveAsync(YouTubeSettings settings, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(settings);
        AsyncServiceScope scope = scopes.CreateAsyncScope();
        await using (scope.ConfigureAwait(false))
        {
            TonTuyauDbContext db = scope.ServiceProvider.GetRequiredService<TonTuyauDbContext>();
            YouTubeSettingsRow? row = await db.YouTubeSettings
                .SingleOrDefaultAsync(s => s.Id == YouTubeSettingsRow.OnlyId, cancellationToken)
                .ConfigureAwait(false);
            if (row is null)
            {
                row = new YouTubeSettingsRow();
                db.YouTubeSettings.Add(row);
            }

            row.Comments = settings.Comments;
            row.CommentsMax = settings.CommentsMax;
            row.PauseSeconds = settings.PauseSeconds;
            row.HoldMinutes = settings.HoldMinutes;
            row.ChangedBy = settings.ChangedBy;
            row.ChangedAt = settings.ChangedAt;
            row.DownloadFormat = settings.DownloadFormat;
            row.SyncEveryMinutes = settings.SyncEveryMinutes;
            row.DefaultVideos = settings.NewChannelTabs.Videos;
            row.DefaultShorts = settings.NewChannelTabs.Shorts;
            row.DefaultStreams = settings.NewChannelTabs.Streams;
            await db
                .SaveChangesAsync(cancellationToken)
                .ConfigureAwait(false);
        }
    }
}
