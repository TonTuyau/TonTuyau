using System.Diagnostics;
using System.Globalization;
using Microsoft.AspNetCore.Mvc;
using TonTuyau.Application.Validation;

namespace TonTuyau.Api.Errors;

/// <summary>The API's ProblemDetails (API_CONTRACT.md → Errors). Messages never carry exception details.</summary>
internal static class Problems
{
    public const string ContentType = "application/problem+json";
    public const string TypeBase = "https://tontuyau/errors/";

    public const string SyncBusySlug = "sync-busy";
    public const string SyncRefusedSlug = "sync-refused";

    public static ProblemDetails Validation(ValidationException exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        Dictionary<string, string[]> errors = exception.Errors
            .GroupBy(e => e.Field, StringComparer.Ordinal)
            .ToDictionary(
                g => g.Key,
                g => g
                    .Select(e => e.Message)
                    .ToArray(),
                StringComparer.Ordinal);
        return new HttpValidationProblemDetails(errors)
        {
            Type = TypeBase + "validation",
            Title = "Invalid request",
            Status = StatusCodes.Status400BadRequest,
            Detail = string.Join(" ", exception.Errors.Select(e => $"{e.Field}: {e.Message}")),
        };
    }

    public static ProblemDetails NotFound(string detail) =>
        Of(StatusCodes.Status404NotFound, "not-found", "Not found", detail);

    public static ProblemDetails Conflict(string detail) =>
        Of(StatusCodes.Status409Conflict, "conflict", "Not now", detail);

    /// <summary>The command line's: another run holds the lock (its exit code 3).</summary>
    public static ProblemDetails SyncBusy(string detail) =>
        Of(StatusCodes.Status409Conflict, SyncBusySlug, "A run is going on", detail);

    /// <summary>The command line's: the run can't be done (its exit code 4).</summary>
    public static ProblemDetails SyncRefused(string detail) =>
        Of(StatusCodes.Status409Conflict, SyncRefusedSlug, "Refused", detail);

    public static ProblemDetails RangeNotSatisfiable(long fileLength) => Of(
        StatusCodes.Status416RangeNotSatisfiable,
        "range-not-satisfiable",
        "Range not satisfiable",
        string.Create(CultureInfo.InvariantCulture, $"The file has {fileLength} bytes."));

    /// <summary>Every refused login, whatever the reason: nothing tells whether the name exists.</summary>
    public static ProblemDetails LoginRefused() =>
        Of(StatusCodes.Status401Unauthorized, "login-refused", "Login refused", "Wrong username or password.");

    public static ProblemDetails NotLoggedIn() =>
        Of(StatusCodes.Status401Unauthorized, "not-logged-in", "Not logged in", "Nobody is logged in.");

    public static ProblemDetails Unexpected() => Of(
        StatusCodes.Status500InternalServerError,
        "internal",
        "Unexpected error",
        "The server couldn't answer. The error is in its log.");

    /// <summary>An answer without a body (unknown route, wrong method…), made a ProblemDetails too.</summary>
    public static ProblemDetails ForStatus(int status) => status switch
    {
        StatusCodes.Status404NotFound => NotFound("Nothing here."),
        StatusCodes.Status405MethodNotAllowed => Of(
            status,
            "method-not-allowed",
            "Method not allowed",
            "This method isn't allowed here."),
        _ => Of(
            status,
            string.Create(CultureInfo.InvariantCulture, $"http-{status}"),
            ReasonPhrase(status),
            null),
    };

    public static Task WriteAsync(HttpContext context, ProblemDetails problem)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(problem);

        // A trace id to find the request in the log, nothing more.
        problem.Extensions["traceId"] = Activity.Current?.Id ?? context.TraceIdentifier;
        context.Response.StatusCode = problem.Status ?? StatusCodes.Status500InternalServerError;
        context.Response.Headers.CacheControl = "no-store";
        return context.Response.WriteAsJsonAsync(problem, problem.GetType(), options: null, ContentType);
    }

    private static ProblemDetails Of(int status, string type, string title, string? detail) => new()
    {
        Type = TypeBase + type,
        Title = title,
        Status = status,
        Detail = detail,
    };

    private static string ReasonPhrase(int status) =>
        Microsoft.AspNetCore.WebUtilities.ReasonPhrases.GetReasonPhrase(status) is { Length: > 0 } phrase
            ? phrase
            : "Error";
}
