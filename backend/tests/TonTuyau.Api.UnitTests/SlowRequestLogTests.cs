using System.Collections.Concurrent;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;
using TonTuyau.Api.Hosting;

namespace TonTuyau.Api.UnitTests;

public sealed class SlowRequestLogTests
{
    private readonly FakeTimeProvider _clock = new();
    private readonly RecordingLogger _logger = new();

    [Fact]
    public async Task A_request_over_a_second_is_logged_with_its_path_status_and_duration()
    {
        await RunAsync("/api/admin/dashboard", TimeSpan.FromMilliseconds(5_250), status: 200);

        Assert.Equal(["Slow request: GET /api/admin/dashboard answered 200 in 5250 ms."], _logger.Messages);
    }

    [Fact]
    public async Task A_quick_request_is_not_logged()
    {
        await RunAsync("/api/admin/youtube", TimeSpan.FromMilliseconds(900), status: 200);

        Assert.Empty(_logger.Messages);
    }

    [Fact]
    public async Task Media_is_not_logged_its_stream_takes_as_long_as_it_plays()
    {
        await RunAsync("/media/vid00000001", TimeSpan.FromMinutes(12), status: 206);

        Assert.Empty(_logger.Messages);
    }

    [Fact]
    public async Task A_slow_request_that_failed_is_logged_too()
    {
        SlowRequestLog log = new(
            _ =>
            {
                _clock.Advance(TimeSpan.FromSeconds(3));
                throw new InvalidOperationException("Elasticsearch timed out.");
            },
            _clock,
            _logger);
        DefaultHttpContext context = new();
        context.Request.Method = "GET";
        context.Request.Path = "/api/videos";

        await Assert.ThrowsAsync<InvalidOperationException>(() => log.InvokeAsync(context));

        Assert.Equal(["Slow request: GET /api/videos answered 200 in 3000 ms."], _logger.Messages);
    }

    private async Task RunAsync(string path, TimeSpan took, int status)
    {
        SlowRequestLog log = new(
            context =>
            {
                _clock.Advance(took);
                context.Response.StatusCode = status;
                return Task.CompletedTask;
            },
            _clock,
            _logger);
        DefaultHttpContext context = new();
        context.Request.Method = "GET";
        context.Request.Path = path;

        await log.InvokeAsync(context);
    }

    private sealed class RecordingLogger : ILogger<SlowRequestLog>
    {
        private readonly ConcurrentQueue<string> _messages = new();

        public IReadOnlyList<string> Messages => [.. _messages];

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter) =>
            _messages.Enqueue(formatter(state, exception));
    }
}
