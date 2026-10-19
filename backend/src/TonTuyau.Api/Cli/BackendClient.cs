using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using TonTuyau.Api.Contracts;
using TonTuyau.Api.Errors;
using TonTuyau.Api.Hosting;
using TonTuyau.Application.Sync;

namespace TonTuyau.Api.Cli;

/// <summary>The command line's side of <see cref="CliController"/>: the backend, through its Unix socket.</summary>
/// <exception cref="HttpRequestException">The backend can't be reached, or answered an error.</exception>
public sealed class BackendClient(HttpClient http)
{
    private static readonly JsonSerializerOptions Json = Options();

    /// <param name="path">The command's endpoint, e.g. <c>cli/import</c>.</param>
    /// <returns>The id of the run it started.</returns>
    /// <exception cref="CliRefusalException">Another run holds the lock, or the run was refused.</exception>
    public async Task<string> StartAsync(string path, CancellationToken cancellationToken)
    {
        using HttpResponseMessage answer = await http
            .PostAsync(new Uri(path, UriKind.Relative), content: null, cancellationToken)
            .ConfigureAwait(false);
        if (answer.StatusCode == HttpStatusCode.Conflict)
        {
            ProblemDetails? problem = await answer.Content
                .ReadFromJsonAsync<ProblemDetails>(Json, cancellationToken)
                .ConfigureAwait(false);
            string detail = problem?.Detail ?? "The backend refused.";
            throw problem?.Type?.EndsWith(Problems.SyncBusySlug, StringComparison.Ordinal) == true
                ? new CliRefusalException(CliRunner.Busy, detail)
                : new CliRefusalException(CliRunner.Refused, "Refused: " + detail);
        }

        answer.EnsureSuccessStatusCode();
        CliStarted? started = await answer.Content
            .ReadFromJsonAsync<CliStarted>(Json, cancellationToken)
            .ConfigureAwait(false);
        return started?.Id ?? throw new HttpRequestException("The backend didn't say which run it started.");
    }

    public async Task<SyncRun> GetRunAsync(string id, CancellationToken cancellationToken) =>
        await http
            .GetFromJsonAsync<SyncRun>(new Uri($"cli/runs/{id}", UriKind.Relative), Json, cancellationToken)
            .ConfigureAwait(false)
        ?? throw new HttpRequestException($"The backend has no run {id}.");

    public async Task<RunLogResponse> GetLogAsync(string id, long after, CancellationToken cancellationToken) =>
        await http
            .GetFromJsonAsync<RunLogResponse>(
                new Uri($"cli/runs/{id}/log?after={after}", UriKind.Relative),
                Json,
                cancellationToken)
            .ConfigureAwait(false)
        ?? throw new HttpRequestException($"The backend has no log for {id}.");

    /// <summary>Asks the backend to stop the run; a run already over is fine (409: nothing to stop).</summary>
    public async Task CancelAsync(string id, CancellationToken cancellationToken)
    {
        using HttpResponseMessage answer = await http
            .PostAsync(new Uri($"cli/runs/{id}/cancel", UriKind.Relative), content: null, cancellationToken)
            .ConfigureAwait(false);
        if (answer.StatusCode != HttpStatusCode.Conflict)
        {
            answer.EnsureSuccessStatusCode();
        }
    }

    /// <summary>The API's own JSON (camelCase, enums as strings).</summary>
    private static JsonSerializerOptions Options()
    {
        JsonSerializerOptions options = new(JsonSerializerDefaults.Web);
        ApiServices.ConfigureJson(options);
        return options;
    }
}
