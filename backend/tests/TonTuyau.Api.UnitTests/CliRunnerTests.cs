using TonTuyau.Api.Cli;
using TonTuyau.Api.Hosting;

namespace TonTuyau.Api.UnitTests;

public sealed class CliRunnerTests
{
    [Theory]
    [InlineData(new[] { "sync", "incremental" }, true)]
    [InlineData(new[] { "index", "rebuild" }, true)]
    [InlineData(new[] { "help" }, true)]
    [InlineData(new[] { "--version" }, true)]
    [InlineData(new[] { "--urls", "http://0.0.0.0:8080" }, false)]
    [InlineData(new string[0], false)]
    public void Only_cli_verbs_start_the_cli(string[] args, bool cli) => Assert.Equal(cli, CliRunner.IsCommand(args));

    [Theory]
    [InlineData("sync")]
    [InlineData("sync everything")]
    [InlineData("sync apply-deletions")]
    [InlineData("index")]
    [InlineData("sync full extra")]
    public async Task Unknown_commands_print_the_usage_and_exit_2(string commandLine)
    {
        using StringWriter output = new();
        string[] args = commandLine.Split(' ');

        int code = await new CliRunner(EmptyServices.Instance, output).RunAsync(
            args,
            TestContext.Current.CancellationToken);

        Assert.Equal(CliRunner.Usage, code);
        Assert.Contains("Usage: tontuyau", output.ToString(), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("sync incremental", true)]
    [InlineData("sync full", true)]
    [InlineData("sync status", true)]
    [InlineData("sync apply-deletions run-x", true)]
    [InlineData("index rebuild", true)]
    [InlineData("help", false)]
    [InlineData("--version", false)]
    [InlineData("sync", false)]
    [InlineData("sync everything", false)]
    [InlineData("sync apply-deletions", false)]
    public void Only_real_commands_need_the_store(string commandLine, bool needsStore) =>
        Assert.Equal(needsStore, CliRunner.NeedsStore(commandLine.Split(' ')));

    [Fact]
    public async Task Help_exits_0()
    {
        using StringWriter output = new();

        int code = await new CliRunner(EmptyServices.Instance, output).RunAsync(
            ["help"],
            TestContext.Current.CancellationToken);

        Assert.Equal(CliRunner.Ok, code);
    }

    [Theory]
    [InlineData("--version")]
    [InlineData("version")]
    public async Task Version_prints_the_version_and_exits_0(string verb)
    {
        using StringWriter output = new();

        int code = await new CliRunner(EmptyServices.Instance, output).RunAsync(
            [verb],
            TestContext.Current.CancellationToken);

        Assert.Equal(CliRunner.Ok, code);
        Assert.Equal("TonTuyau " + AppVersion.Value, output.ToString().TrimEnd());
        Assert.True(CliRunner.IsVersion([verb]));
    }

    private sealed class EmptyServices : IServiceProvider
    {
        public static readonly EmptyServices Instance = new();

        public object? GetService(Type serviceType) => null;
    }
}
