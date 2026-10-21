using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using TonTuyau.Application.Configuration;
using TonTuyau.Application.Streaming;

namespace TonTuyau.Infrastructure.Streaming;

/// <summary>
/// The viewings going on (C3, question 9): one ffmpeg run each (<see cref="StreamCopy"/>), from the part asked for,
/// going on with the next ones as they're asked for; a part off its way (a seek) starts a new run from there. A viewing
/// nobody asks anything of for <see cref="Idle"/> is stopped; at most <see cref="MaxRunning"/> runs at once (past it,
/// the least recently asked one stops: asked again, it starts again). The init segment, the same for every viewing of
/// a file, is kept.
/// </summary>
internal sealed partial class FfmpegStreamSessions : IStreamSessions, IDisposable
{
    public const int MaxRunning = 4;

    public static readonly TimeSpan Idle = TimeSpan.FromSeconds(60);

    /// <summary>Init segments kept (about 1 KB each): past it, they're forgotten and made again.</summary>
    private const int MaxInits = 512;

    private readonly Lock _gate = new();
    private readonly Dictionary<string, Viewing> _viewings = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<(string VideoId, long FileSize), byte[]> _inits = new();
    private readonly string _ffmpeg;
    private readonly TimeProvider _clock;
    private readonly ILogger<FfmpegStreamSessions> _logger;
    private readonly ITimer _sweep;

    public FfmpegStreamSessions(TonTuyauOptions options, TimeProvider clock, ILogger<FfmpegStreamSessions> logger)
        : this(options?.YouTube.FfmpegPath ?? throw new ArgumentNullException(nameof(options)), clock, logger)
    {
    }

    /// <param name="ffmpeg">The ffmpeg to run (the image's, <c>TONTUYAU_FFMPEG_PATH</c>).</param>
    internal FfmpegStreamSessions(string ffmpeg, TimeProvider clock, ILogger<FfmpegStreamSessions> logger)
    {
        ArgumentNullException.ThrowIfNull(clock);
        _ffmpeg = ffmpeg;
        _clock = clock;
        _logger = logger;
        _sweep = clock.CreateTimer(_ => Sweep(), null, Idle / 4, Idle / 4);
    }

    public async Task<byte[]> InitAsync(StreamSource source, string session, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (_inits.TryGetValue((source.VideoId, source.FileSize), out byte[]? kept))
        {
            return kept;
        }

        StreamCopy copy;
        lock (_gate)
        {
            Viewing viewing = ViewingOf(source, session);
            // The first part comes with it: prepared before play (question 9).
            copy = viewing.Copy is { Running: true } running ? running : Restart(viewing, 0);
        }

        byte[] init = await copy.Init.WaitAsync(cancellationToken).ConfigureAwait(false);
        if (_inits.Count >= MaxInits)
        {
            _inits.Clear();
        }

        _inits[(source.VideoId, source.FileSize)] = init;
        return init;
    }

    public Task<byte[]> PartAsync(StreamSource source, string session, int index, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(source);
        Task<byte[]> part;
        lock (_gate)
        {
            Viewing viewing = ViewingOf(source, session);
            StreamCopy copy = viewing.Copy is not null && viewing.Copy.Serves(index)
                ? viewing.Copy
                : Restart(viewing, index);
            part = copy.PartAsync(index);
        }

        return part.WaitAsync(cancellationToken);
    }

    public void Close(string session)
    {
        lock (_gate)
        {
            if (_viewings.Remove(session, out Viewing? viewing))
            {
                viewing.Copy?.Dispose();
            }
        }
    }

    public void Dispose()
    {
        _sweep.Dispose();
        lock (_gate)
        {
            foreach (Viewing viewing in _viewings.Values)
            {
                viewing.Copy?.Dispose();
            }

            _viewings.Clear();
        }
    }

    /// <summary>The session's viewing, seen now; another video in the same session starts afresh. Under the lock.</summary>
    private Viewing ViewingOf(StreamSource source, string session)
    {
        if (!_viewings.TryGetValue(session, out Viewing? viewing)
            || viewing.Source.VideoId != source.VideoId
            || viewing.Source.FileSize != source.FileSize)
        {
            viewing?.Copy?.Dispose();
            viewing = new Viewing(source);
            _viewings[session] = viewing;
        }

        viewing.LastSeen = _clock.GetUtcNow();
        return viewing;
    }

    /// <summary>A new run from this part, the one before stopped; past the cap, the least recently asked stops.</summary>
    private StreamCopy Restart(Viewing viewing, int index)
    {
        viewing.Copy?.Dispose();
        viewing.Copy = StreamCopy.Start(_ffmpeg, viewing.Source, index, _logger);
        List<Viewing> running = [.. _viewings.Values.Where(v => v.Copy is { Running: true }).OrderBy(v => v.LastSeen)];
        foreach (Viewing oldest in running.Take(Math.Max(0, running.Count - MaxRunning)))
        {
            if (oldest != viewing)
            {
                LogCapped(oldest.Source.VideoId);
                oldest.Copy?.Dispose();
                oldest.Copy = null;
            }
        }

        return viewing.Copy;
    }

    /// <summary>The viewings nobody asked anything of for <see cref="Idle"/>: their viewer left.</summary>
    private void Sweep()
    {
        DateTimeOffset before = _clock.GetUtcNow() - Idle;
        lock (_gate)
        {
            foreach ((string session, Viewing viewing) in _viewings.Where(v => v.Value.LastSeen < before).ToArray())
            {
                viewing.Copy?.Dispose();
                _viewings.Remove(session);
            }
        }
    }

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "Too many videos played in parts at once: the viewing of {VideoId} stops (asked again, it resumes).")]
    private partial void LogCapped(string videoId);

    private sealed class Viewing(StreamSource source)
    {
        public StreamSource Source { get; } = source;

        public StreamCopy? Copy { get; set; }

        public DateTimeOffset LastSeen { get; set; }
    }
}
