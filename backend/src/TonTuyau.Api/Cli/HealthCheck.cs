namespace TonTuyau.Api.Cli;

/// <summary>
/// <c>tontuyau health</c> (1.5.1): asks the backend running in this container whether it answers (its <c>/health</c>,
/// on 127.0.0.1): exit code 0 when it does, 1 otherwise. The container's health check, since the image has neither a
/// shell nor curl. No configuration needed, like <c>--version</c>.
/// </summary>
internal static class HealthCheck
{
    public static readonly TimeSpan Timeout = TimeSpan.FromSeconds(4);

    /// <summary>The backend's port here: the first of <c>ASPNETCORE_HTTP_PORTS</c> (8080 in the image).</summary>
    public static int Port(string? httpPorts)
    {
        string first = (httpPorts ?? "").Split([';', ','], StringSplitOptions.TrimEntries)[0];
        return int.TryParse(first, out int port) ? port : 8080;
    }

    public static async Task<int> AskAsync(int port, TextWriter error)
    {
        ArgumentNullException.ThrowIfNull(error);
        using HttpClient http = new() { Timeout = Timeout };
        try
        {
            using HttpResponseMessage answer = await http
                .GetAsync(new Uri($"http://127.0.0.1:{port}/health"))
                .ConfigureAwait(false);
            if (answer.IsSuccessStatusCode)
            {
                return CliRunner.Ok;
            }

            await error.WriteLineAsync($"The backend answered {(int)answer.StatusCode}.").ConfigureAwait(false);
            return CliRunner.RunFailed;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            await error.WriteLineAsync("The backend doesn't answer: " + ex.Message).ConfigureAwait(false);
            return CliRunner.RunFailed;
        }
    }
}
