using System.Text.RegularExpressions;
using Microsoft.Extensions.DependencyInjection;
using TonTuyau.Api.Cli;
using TonTuyau.Application.Sync;
using TonTuyau.IntegrationTests.Support;

namespace TonTuyau.IntegrationTests.Steps;

[Binding]
public sealed partial class CliSteps(ScenarioState state) : IDisposable
{
    private readonly StringWriter _output = new();
    private int? _exitCode;

    [When("I run {string}")]
    public async Task Run(string commandLine)
    {
        string[] args = commandLine.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal("tontuyau", args[0]);
        _exitCode = await new CliRunner(state.Backend.Services, _output).RunAsync(args[1..], CancellationToken.None);
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
    /// line is the run's id).
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

        Assert.All(lines[..report], l => Assert.Matches(ProgressLine(), l));
    }

    public void Dispose() => _output.Dispose();

    [GeneratedRegex(@"\A\[\d\d:\d\d\] [a-z]")]
    private static partial Regex ProgressLine();
}
