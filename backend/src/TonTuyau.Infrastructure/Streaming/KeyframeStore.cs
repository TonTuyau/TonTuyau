using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TonTuyau.Application.Streaming;
using TonTuyau.Infrastructure.Accounts;

namespace TonTuyau.Infrastructure.Streaming;

/// <summary>The videos' keyframe times in PostgreSQL (C3). A singleton, a database context per call.</summary>
internal sealed class KeyframeStore(IServiceScopeFactory scopes) : IKeyframeStore
{
    public async Task<IReadOnlyList<double>?> GetAsync(
        string videoId,
        long fileSize,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(videoId);
        VideoKeyframesRow? row = await WithContextAsync(db => db.VideoKeyframes
            .AsNoTracking()
            .FirstOrDefaultAsync(k => k.VideoId == videoId, cancellationToken))
            .ConfigureAwait(false);
        return row is null || row.FileSize != fileSize ? null : [.. row.TimesMs.Select(ms => ms / 1000.0)];
    }

    public async Task SaveAsync(
        string videoId,
        long fileSize,
        IReadOnlyList<double> times,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(videoId);
        ArgumentNullException.ThrowIfNull(times);
        int[] ms = [.. times.Select(t => (int)Math.Round(t * 1000))];
        await WithContextAsync(async db =>
        {
            VideoKeyframesRow? row = await db.VideoKeyframes
                .FirstOrDefaultAsync(k => k.VideoId == videoId, cancellationToken)
                .ConfigureAwait(false);
            if (row is null)
            {
                db.VideoKeyframes.Add(new VideoKeyframesRow { VideoId = videoId, FileSize = fileSize, TimesMs = ms });
            }
            else
            {
                row.FileSize = fileSize;
                row.TimesMs = ms;
            }

            return await db
                .SaveChangesAsync(cancellationToken)
                .ConfigureAwait(false);
        })
            .ConfigureAwait(false);
    }

    private async Task<T> WithContextAsync<T>(Func<TonTuyauDbContext, Task<T>> work)
    {
        AsyncServiceScope scope = scopes.CreateAsyncScope();
        await using (scope.ConfigureAwait(false))
        {
            return await work(scope.ServiceProvider.GetRequiredService<TonTuyauDbContext>()).ConfigureAwait(false);
        }
    }
}
