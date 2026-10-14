namespace TonTuyau.Application.RunLogs;

/// <summary>The runs' logs, in PostgreSQL. Kept forever (1.4).</summary>
public interface IRunLogRepository
{
    /// <summary>Saves the lines, in their order.</summary>
    Task AddAsync(IReadOnlyList<RunLogLine> lines, CancellationToken cancellationToken);

    /// <returns>
    /// The run's lines after <paramref name="afterId"/> (0: from the first), oldest first, at most
    /// <paramref name="count"/>.
    /// </returns>
    Task<IReadOnlyList<RunLogLine>> GetAsync(
        string runId,
        long afterId,
        int count,
        CancellationToken cancellationToken);
}
