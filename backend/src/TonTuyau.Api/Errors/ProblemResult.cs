using Microsoft.AspNetCore.Mvc;

namespace TonTuyau.Api.Errors;

/// <summary>An action's answer that is a ProblemDetails, written like every other one (<see cref="Problems"/>).</summary>
internal sealed class ProblemResult(ProblemDetails problem) : IActionResult
{
    public Task ExecuteResultAsync(ActionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return Problems.WriteAsync(context.HttpContext, problem);
    }
}
