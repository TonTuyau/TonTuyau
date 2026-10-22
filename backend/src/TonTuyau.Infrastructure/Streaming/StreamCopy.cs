using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using Microsoft.Extensions.Logging;
using TonTuyau.Application.Streaming;

namespace TonTuyau.Infrastructure.Streaming;

/// <summary>
/// One ffmpeg run copying a file into parts (C3), from one part on: its output, a fragmented MP4 whose timestamps are
/// the file's own (<c>-copyts</c> and <c>frag_discont</c>: the parts of two runs join without a jump), cut into the
/// parts of the playlist by the time of each fragment's video. It stays at most <see cref="Ahead"/> parts ahead of the
/// furthest one asked for: past it, its output isn't read, and ffmpeg waits.
/// </summary>
internal sealed partial class StreamCopy : IDisposable
{
    /// <summary>How many parts it copies ahead of the furthest one asked for.</summary>
    public const int Ahead = 2;

    /// <summary>A fragment this close before a part's start (decode against presentation times) is that part's.</summary>
    public const double Tolerance = 0.25;

    private readonly Lock _gate = new();
    private readonly Dictionary<int, TaskCompletionSource<byte[]>> _parts = [];
    private readonly TaskCompletionSource<byte[]> _init = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly SemaphoreSlim _room = new(0);
    private readonly CancellationTokenSource _stop = new();
    private readonly Queue<string> _errors = new();
    private readonly ILogger _logger;
    private Process? _process;
    private int _wanted;
    private int _filling;
    private int _oldest;
    private bool _ended;

    private StreamCopy(StreamSource source, int first, ILogger logger)
    {
        Source = source;
        First = first;
        _wanted = first;
        _filling = first;
        _oldest = first;
        _logger = logger;
    }

    public StreamSource Source { get; }

    /// <summary>The part it started at.</summary>
    public int First { get; }

    /// <summary>The init segment, once ffmpeg wrote it.</summary>
    public Task<byte[]> Init => _init.Task;

    public bool Running
    {
        get
        {
            lock (_gate)
            {
                return !_ended;
            }
        }
    }

    /// <summary>Starts ffmpeg on the file from part <paramref name="first"/>.</summary>
    public static StreamCopy Start(string ffmpeg, StreamSource source, int first, ILogger logger)
    {
        StreamCopy copy = new(source, first, logger);
        copy.Run(ffmpeg);
        return copy;
    }

    /// <summary>
    /// Whether part <paramref name="index"/> is on this run's way: from where it started, not dropped yet, and not
    /// much further than where it is (further, a new run from there is quicker).
    /// </summary>
    public bool Serves(int index)
    {
        lock (_gate)
        {
            if (index < Math.Max(First, _oldest))
            {
                return false;
            }

            if (_parts.TryGetValue(index, out TaskCompletionSource<byte[]>? part) && part.Task.IsCompletedSuccessfully)
            {
                return true;
            }

            return !_ended && index <= Math.Max(_filling, _wanted) + Ahead + 1;
        }
    }

    /// <summary>A part, once copied; asking for it lets the run go on up to <see cref="Ahead"/> parts past it.</summary>
    public Task<byte[]> PartAsync(int index)
    {
        lock (_gate)
        {
            if (index > _wanted)
            {
                _wanted = index;
                _room.Release();
            }

            // What came before the one asked for is played: not kept (asked again, a new run copies it).
            foreach (int done in _parts.Keys.Where(i => i < index - 1).ToArray())
            {
                _parts.Remove(done);
                _oldest = Math.Max(_oldest, done + 1);
            }

            return Part(index).Task;
        }
    }

    /// <summary>Stops ffmpeg; what waited for its parts is canceled.</summary>
    public void Dispose()
    {
        lock (_gate)
        {
            _ended = true;
            foreach (TaskCompletionSource<byte[]> part in _parts.Values)
            {
                part.TrySetCanceled();
            }
        }

        _init.TrySetCanceled();
        if (!_stop.IsCancellationRequested)
        {
            _stop.Cancel();
        }

        Kill();
    }

    private TaskCompletionSource<byte[]> Part(int index)
    {
        if (!_parts.TryGetValue(index, out TaskCompletionSource<byte[]>? part))
        {
            part = new TaskCompletionSource<byte[]>(TaskCreationOptions.RunContinuationsAsynchronously);
            _parts[index] = part;
        }

        return part;
    }

    private void Run(string ffmpeg)
    {
        double start = Source.Parts[First].Start;
        ProcessStartInfo info = new(ffmpeg)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        List<string> arguments = ["-nostdin", "-hide_banner", "-loglevel", "error"];
        if (start > 0)
        {
            // Just past the keyframe: ffmpeg starts at the keyframe at or before it (a Matroska file may start one
            // earlier: what comes before the part is left out below).
            arguments.AddRange(["-ss", (start + 0.001).ToString("0.000", CultureInfo.InvariantCulture)]);
        }

        arguments.AddRange(
        [
            "-copyts", "-i", Source.FullPath, "-map", "0:v:0", "-map", "0:a:0?", "-c", "copy", "-dn", "-sn",
            "-avoid_negative_ts", "disabled", "-f", "mp4",
            "-movflags", "frag_keyframe+empty_moov+default_base_moof+frag_discont", "pipe:1",
        ]);
        foreach (string argument in arguments)
        {
            info.ArgumentList.Add(argument);
        }

        Process process = new() { StartInfo = info };
        process.ErrorDataReceived += (_, line) =>
        {
            if (line.Data is { Length: > 0 } text)
            {
                lock (_errors)
                {
                    _errors.Enqueue(text);
                    if (_errors.Count > 5)
                    {
                        _errors.Dequeue();
                    }
                }
            }
        };
        try
        {
            process.Start();
        }
        catch (Win32Exception ex)
        {
            process.Dispose();
            Fail(new StreamFailedException("ffmpeg can't be run here.", ex));
            return;
        }

        process.BeginErrorReadLine();
        _process = process;
        LogStarted(Source.VideoId, First, start);
        _ = Task.Run(() => ReadAsync(process));
    }

    private async Task ReadAsync(Process process)
    {
        CancellationToken stop = _stop.Token;
        try
        {
            FragmentedMp4Reader reader = new(process.StandardOutput.BaseStream);
            byte[] init = await reader.ReadInitAsync(stop).ConfigureAwait(false)
                ?? throw new StreamFailedException(Errors("ffmpeg wrote nothing"));
            _init.TrySetResult(init);
            int filling = First;
            List<byte[]> buffer = [];
            while (await reader.ReadFragmentAsync(stop).ConfigureAwait(false) is Mp4Fragment fragment)
            {
                int index = PartOf(fragment.Time ?? Source.Parts[filling].Start);
                if (index < First)
                {
                    // Before the part it was started for (a Matroska file starts at the keyframe before).
                    continue;
                }

                if (index > filling && buffer.Count > 0)
                {
                    Done(filling, buffer);
                    buffer = [];
                }

                filling = Math.Max(filling, index);
                await RoomForAsync(filling, stop).ConfigureAwait(false);
                buffer.Add(fragment.Bytes);
            }

            if (buffer.Count > 0)
            {
                Done(filling, buffer);
            }

            await process.WaitForExitAsync(stop).ConfigureAwait(false);
            Ended(process.ExitCode == 0
                ? null
                : new StreamFailedException(Errors($"ffmpeg ended ({process.ExitCode})")));
        }
        catch (OperationCanceledException) when (stop.IsCancellationRequested)
        {
            // Stopped: Dispose canceled what waited.
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or StreamFailedException)
        {
            LogFailed(ex, Source.VideoId, First);
            Fail(ex as StreamFailedException ?? new StreamFailedException(Errors(ex.Message), ex));
        }
        finally
        {
            Kill();
        }
    }

    /// <summary>Waits, ffmpeg's output unread, while the part is too far ahead of the furthest one asked for.</summary>
    private async Task RoomForAsync(int index, CancellationToken stop)
    {
        while (true)
        {
            lock (_gate)
            {
                _filling = index;
                if (index <= _wanted + Ahead)
                {
                    return;
                }
            }

            await _room.WaitAsync(stop).ConfigureAwait(false);
        }
    }

    /// <summary>The part a fragment starting at this time belongs to: the last one starting at or before it.</summary>
    private int PartOf(double time)
    {
        IReadOnlyList<StreamPart> parts = Source.Parts;
        int low = 0;
        int high = parts.Count - 1;
        while (low < high)
        {
            int middle = (low + high + 1) / 2;
            if (parts[middle].Start <= time + Tolerance)
            {
                low = middle;
            }
            else
            {
                high = middle - 1;
            }
        }

        return low;
    }

    private void Done(int index, List<byte[]> fragments)
    {
        byte[] bytes = new byte[fragments.Sum(f => f.Length)];
        int at = 0;
        foreach (byte[] fragment in fragments)
        {
            fragment.CopyTo(bytes, at);
            at += fragment.Length;
        }

        lock (_gate)
        {
            if (index >= _oldest)
            {
                Part(index).TrySetResult(bytes);
            }
        }
    }

    /// <summary>The end of the output: what waits for a part it didn't copy fails (a new run copies it).</summary>
    private void Ended(StreamFailedException? failure)
    {
        lock (_gate)
        {
            _ended = true;
            foreach ((int index, TaskCompletionSource<byte[]> part) in _parts)
            {
                part.TrySetException(failure ?? new StreamFailedException($"No part {index} in the output."));
            }
        }

        _init.TrySetException(failure ?? new StreamFailedException("No init segment in the output."));
    }

    private void Fail(Exception failure)
    {
        lock (_gate)
        {
            _ended = true;
            foreach (TaskCompletionSource<byte[]> part in _parts.Values)
            {
                part.TrySetException(failure);
            }
        }

        _init.TrySetException(failure);
    }

    private string Errors(string what)
    {
        lock (_errors)
        {
            return _errors.Count == 0 ? $"{what}." : $"{what}: {string.Join(" ", _errors)}";
        }
    }

    private void Kill()
    {
        Process? process = Interlocked.Exchange(ref _process, null);
        if (process is null)
        {
            return;
        }

        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch (InvalidOperationException)
        {
            // Ended meanwhile: nothing to stop.
        }
        finally
        {
            process.Dispose();
        }
    }

    [LoggerMessage(Level = LogLevel.Debug, Message = "Copying {VideoId} into parts from part {Part} ({Start} s).")]
    private partial void LogStarted(string videoId, int part, double start);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Copying {VideoId} into parts from part {Part} failed.")]
    private partial void LogFailed(Exception exception, string videoId, int part);
}
