using System.Text;
using System.Text.Json;
using TonTuyau.Application.Library;
using TonTuyau.Application.Sync;

namespace TonTuyau.Infrastructure.Storage;

/// <summary><c>&lt;root&gt;/&lt;run&gt;-deletions.jsonl</c>: one JSON object per line, readable with any text tool.</summary>
internal sealed class DeletionLog(string root) : IDeletionLog
{
    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    public async Task<string> WriteAsync(
        string runId,
        IReadOnlyList<DeletionEntry> entries,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(entries);
        string file = FileFor(runId);
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        StringBuilder text = new();
        foreach (DeletionEntry entry in entries)
        {
            text.Append(JsonSerializer.Serialize(entry, Json));
            text.Append('\n');
        }

        await File.WriteAllTextAsync(file, text.ToString(), cancellationToken).ConfigureAwait(false);
        return file;
    }

    public async Task<IReadOnlyList<DeletionEntry>?> ReadAsync(string runId, CancellationToken cancellationToken)
    {
        string file = FileFor(runId);
        if (!File.Exists(file))
        {
            return null;
        }

        string[] lines = await File.ReadAllLinesAsync(file, cancellationToken).ConfigureAwait(false);
        return lines
            .Where(l => l.Length > 0)
            .Select(l => JsonSerializer.Deserialize<DeletionEntry>(l, Json)!)
            .ToArray();
    }

    internal string FileFor(string runId) =>
        LibraryPaths.IsRunId(runId)
            ? Path.Combine(Path.GetFullPath(root), $"{runId}-deletions.jsonl")
            : throw new ArgumentException($"'{runId}' isn't a run id.", nameof(runId));
}
