namespace TonTuyau.Api.Cli;

/// <summary>The backend didn't start the run: the command ends with this message and exit code.</summary>
public sealed class CliRefusalException(int exitCode, string message) : Exception(message)
{
    public int ExitCode { get; } = exitCode;
}
