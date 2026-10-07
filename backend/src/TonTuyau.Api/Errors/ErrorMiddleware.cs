using Microsoft.AspNetCore.Mvc;
using TonTuyau.Application.Browse;
using TonTuyau.Application.Media;
using TonTuyau.Application.Validation;

namespace TonTuyau.Api.Errors;

/// <summary>
/// The one place exceptions become HTTP answers: RFC 9457 ProblemDetails, <c>application/problem+json</c>.
/// Validation → 400, not found → 404, bad range → 416, anything else → 500 without internals (logged).
/// </summary>
internal sealed partial class ErrorMiddleware(RequestDelegate next, ILogger<ErrorMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context).ConfigureAwait(false);
        }
        catch (Exception ex) when (context.RequestAborted.IsCancellationRequested
            && ex is OperationCanceledException or IOException)
        {
            // The client went away (a player seeking drops its previous range request all the time): there's
            // nobody to answer and nothing went wrong. Control flow, not an error.
        }
        catch (ValidationException ex)
        {
            await WriteAsync(context, Problems.Validation(ex)).ConfigureAwait(false);
        }
        catch (NotFoundException ex)
        {
            await WriteAsync(context, Problems.NotFound(ex.Message)).ConfigureAwait(false);
        }
        catch (RangeNotSatisfiableException ex)
        {
            if (!context.Response.HasStarted)
            {
                context.Response.Headers.ContentRange = ex.ContentRange;
            }

            await WriteAsync(context, Problems.RangeNotSatisfiable(ex.FileLength)).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            LogUnexpected(ex, context.Request.Method, context.Request.Path);
            await WriteAsync(context, Problems.Unexpected()).ConfigureAwait(false);
        }
    }

    private async Task WriteAsync(HttpContext context, ProblemDetails problem)
    {
        if (context.Response.HasStarted)
        {
            // Headers are gone (a file was being sent): the only honest answer left is to cut the connection.
            LogTooLate(problem.Status ?? 0, context.Request.Path);
            context.Abort();
            return;
        }

        await Problems.WriteAsync(context, problem).ConfigureAwait(false);
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Unexpected error answering {Method} {Path}.")]
    private partial void LogUnexpected(Exception exception, string method, string path);

    [LoggerMessage(Level = LogLevel.Error, Message = "Error {Status} after the answer to {Path} started: aborted.")]
    private partial void LogTooLate(int status, string path);
}
