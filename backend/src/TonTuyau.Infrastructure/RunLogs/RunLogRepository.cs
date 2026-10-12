using Microsoft.EntityFrameworkCore;
using TonTuyau.Application.RunLogs;
using TonTuyau.Infrastructure.Accounts;

namespace TonTuyau.Infrastructure.RunLogs;

/// <summary>The runs' logs' database calls (EF Core, PostgreSQL).</summary>
internal sealed class RunLogRepository(TonTuyauDbContext db) : IRunLogRepository
{
    public async Task AddAsync(IReadOnlyList<RunLogLine> lines, CancellationToken cancellationToken)
    {
        db.RunLogLines.AddRange(lines.Select(l => (RunLogRow)l));
        await db
            .SaveChangesAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<RunLogLine>> GetAsync(
        string runId,
        long afterId,
        int count,
        CancellationToken cancellationToken)
    {
        List<RunLogRow> rows = await db.RunLogLines
            .AsNoTracking()
            .Where(l => l.RunId == runId && l.Id > afterId)
            .OrderBy(l => l.Id)
            .Take(count)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        return [.. rows.Select(r => (RunLogLine)r)];
    }
}
