using Microsoft.Extensions.DependencyInjection;
using TonTuyau.Application.RunLogs;
using TonTuyau.Application.Sync;
using TonTuyau.IntegrationTests.Support;

namespace TonTuyau.IntegrationTests.Steps;

[Binding]
public sealed class RunLogSteps(ScenarioState state)
{
    /// <summary>The lines are written a second or so after they're logged: this long at most.</summary>
    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(15);

    private string? _runId;
    private IReadOnlyList<RunLogLine> _lines = [];

    /// <summary>
    /// Lines whose text contains each row's, at that level, in that order (other lines may come between). Waits for the
    /// last one to be written.
    /// </summary>
    [Then("the last run's log has, in order, lines with:")]
    public async Task HasInOrder(DataTable table)
    {
        (RunLogLevel Level, string Text)[] expected = table.Rows
            .Select(r => (Enum.Parse<RunLogLevel>(r["level"], ignoreCase: true), r["text"]))
            .ToArray();
        DateTime deadline = DateTime.UtcNow + Patience;
        while (true)
        {
            await ReadAsync();
            int found = 0;
            foreach (RunLogLine line in _lines)
            {
                if (found < expected.Length
                    && line.Level == expected[found].Level
                    && line.Message.Contains(expected[found].Text, StringComparison.Ordinal))
                {
                    found++;
                }
            }

            if (found == expected.Length)
            {
                return;
            }

            if (DateTime.UtcNow > deadline)
            {
                string seen = string.Join("\n", _lines.Select(l => $"{l.Level} {l.Message}"));
                Assert.Fail($"Only {found} of the {expected.Length} lines, in order, in the log of {_runId}:\n{seen}");
            }

            await Task.Delay(250);
        }
    }

    [Then("every line of the last run's log is that run's")]
    public void AllOfThatRun()
    {
        Assert.NotEmpty(_lines);
        Assert.All(_lines, l => Assert.Equal(_runId, l.RunId));
        Assert.Equal(_lines.OrderBy(l => l.Id).Select(l => l.Id), _lines.Select(l => l.Id));
    }

    [Then("no line of the last run's log is about an HTTP request")]
    public void NoHttpLines() =>
        Assert.DoesNotContain(_lines, l => l.Message.Contains("HTTP request", StringComparison.OrdinalIgnoreCase));

    private async Task ReadAsync()
    {
        IServiceProvider services = state.Backend.Services;
        IReadOnlyList<SyncRun> runs = await services
            .GetRequiredService<ISyncStateStore>()
            .GetRecentRunsAsync(1, CancellationToken.None);
        _runId = runs[0].Id;
        AsyncServiceScope scope = services.CreateAsyncScope();
        await using (scope)
        {
            _lines = await scope.ServiceProvider
                .GetRequiredService<IRunLogRepository>()
                .GetAsync(_runId, 0, 10_000, CancellationToken.None);
        }
    }
}
