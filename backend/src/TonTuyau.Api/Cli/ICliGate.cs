namespace TonTuyau.Api.Cli;

/// <summary>Whether a request may use the command line's endpoints (<see cref="CliController"/>).</summary>
public interface ICliGate
{
    bool Allows(HttpContext context);
}
