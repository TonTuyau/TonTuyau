using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.Extensions.DependencyInjection;
using TonTuyau.Api.Cli;
using TonTuyau.Application.Sync;
using TonTuyau.IntegrationTests.Support;

namespace TonTuyau.IntegrationTests.Steps;

[Binding]
public sealed partial class CliSteps(ScenarioState state, SyncSteps sync) : IDisposable
{
    private readonly StringWriter _output = new();
    private readonly CancellationTokenSource _ctrlC = new();
    private int? _exitCode;

    /// <summary>
    /// As Ctrl-C does in a terminal: the command's cancellation, at a known point of the run. TubeArchivist then takes
    /// its time to answer: the command line sees Ctrl-C within half a second, and asks the backend to cancel.
    /// </summary>
    [Given("Ctrl-C is pressed right before TubeArchivist answers page {int}")]
    public void CtrlCBeforePage(int page) =>
        sync.Fake.BeforeAnswer[$"page={page.ToString(CultureInfo.InvariantCulture)}"] = () =>
        {
            _ctrlC.Cancel();
            Thread.Sleep(TimeSpan.FromSeconds(2));
        };

    [When("I run {string}")]
    public async Task Run(string commandLine)
    {
        string[] args = commandLine.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal("tontuyau", args[0]);
        // The backend's in-memory server stands for its Unix socket (it passes the CLI's gate the same way).
        using HttpClient backend = state.Backend.CreateClient();
        _exitCode = await new CliRunner(state.Backend.Services, _output, new BackendClient(backend))
            .RunAsync(args[1..], _ctrlC.Token);
    }

    [When("I run {string} for the latest full run")]
    public async Task RunForLatestFullRun(string commandLine)
    {
        SyncRun? latest = await state.Backend.Services
            .GetRequiredService<ISyncStateStore>()
            .GetLatestFullRunAsync(CancellationToken.None);
        Assert.NotNull(latest);
        await Run($"{commandLine} {latest.Id}");
    }

    [Then("the command exits with code {int}")]
    public void ExitCode(int code) => Assert.True(code == _exitCode, $"exit {_exitCode}:\n{_output}");

    [Then("the output mentions {string}")]
    public void OutputMentions(string text) => Assert.Contains(text, _output.ToString(), StringComparison.Ordinal);

    /// <summary>
    /// Progress lines (<c>[mm:ss] phase  detail</c>) for each phase, in this order, all before the report (its first
    /// line is the run's id). Since 1.4 they come from the run's log, among its other lines (its start, its end).
    /// </summary>
    [Then("the progress shows the phases {string} before the report")]
    public void ProgressBeforeReport(string phases)
    {
        string[] lines = _output.ToString().ReplaceLineEndings("\n").Split('\n');
        int report = Array.FindIndex(lines, l => l.StartsWith("run-", StringComparison.Ordinal));
        Assert.True(report > 0, $"no report, or nothing before it:\n{_output}");
        int previous = -1;
        foreach (string phase in phases.Split(", "))
        {
            int at = Array.FindIndex(
                lines,
                l => ProgressLine().IsMatch(l) && l[8..].StartsWith(phase + " ", StringComparison.Ordinal));
            Assert.True(at > previous && at < report, $"no progress line for {phase} in its place:\n{_output}");
            previous = at;
        }

    }

    public void Dispose()
    {
        _output.Dispose();
        _ctrlC.Dispose();
    }

    [GeneratedRegex(@"\A\[\d\d:\d\d\] [a-z]")]
    private static partial Regex ProgressLine();
}
