namespace TonTuyau.Api.Hosting;

/// <summary>
/// Logs every request that takes more than <see cref="Threshold"/> (1.5): its method, path, status and duration, a
/// warning to find what's slow (pages slow while a refresh goes on, for instance). Media isn't: streaming a video
/// takes as long as it plays. The query string isn't logged, the path is enough.
/// </summary>
internal sealed partial class SlowRequestLog(RequestDelegate next, TimeProvider clock, ILogger<SlowRequestLog> logger)
{
    public static readonly TimeSpan Threshold = TimeSpan.FromSeconds(1);

    public async Task InvokeAsync(HttpContext context)
    {
        long start = clock.GetTimestamp();
        try
        {
            await next(context).ConfigureAwait(false);
        }
        finally
        {
            TimeSpan took = clock.GetElapsedTime(start);
            if (took > Threshold && !context.Request.Path.StartsWithSegments("/media", StringComparison.Ordinal))
            {
                LogSlow(
                    context.Request.Method,
                    context.Request.Path.Value ?? "",
                    context.Response.StatusCode,
                    (long)took.TotalMilliseconds);
            }
        }
    }

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "Slow request: {Method} {Path} answered {Status} in {Milliseconds} ms.")]
    private partial void LogSlow(string method, string path, int status, long milliseconds);
}
