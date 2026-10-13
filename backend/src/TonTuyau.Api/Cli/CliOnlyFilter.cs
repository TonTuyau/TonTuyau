using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using TonTuyau.Api.Errors;

namespace TonTuyau.Api.Cli;

/// <summary>The command line's endpoints answer anyone else (<see cref="ICliGate"/>) a 404: they don't exist.</summary>
internal sealed class CliOnlyFilter(ICliGate gate) : IResourceFilter
{
    public void OnResourceExecuting(ResourceExecutingContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (!gate.Allows(context.HttpContext))
        {
            context.Result = new ProblemResult(Problems.NotFound("Nothing here."));
        }
    }

    public void OnResourceExecuted(ResourceExecutedContext context)
    {
    }
}
