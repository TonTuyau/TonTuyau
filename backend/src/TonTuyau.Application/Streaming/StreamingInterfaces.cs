namespace TonTuyau.Application.Streaming;

/// <summary>Reads where a video file's keyframes are, from the file's own index (C3: no full read of the file).</summary>
public interface IKeyframeReader
{
    /// <returns>Their times in seconds, in order; null when the file has no index to read them from.</returns>
    Task<IReadOnlyList<double>?> ReadAsync(string fullPath, CancellationToken cancellationToken);
}

/// <summary>The keyframe times read, kept with the size of the file they were read from (another file: read again).</summary>
public interface IKeyframeStore
{
    /// <returns>Those read from a file of this size; null when none were (or from another file). Empty: no index.</returns>
    Task<IReadOnlyList<double>?> GetAsync(string videoId, long fileSize, CancellationToken cancellationToken);

    /// <param name="times">Empty: the file has no index, it plays whole.</param>
    Task SaveAsync(string videoId, long fileSize, IReadOnlyList<double> times, CancellationToken cancellationToken);
}

/// <summary>A video played in parts: its file and its parts.</summary>
public sealed record StreamSource(string VideoId, string FullPath, long FileSize, IReadOnlyList<StreamPart> Parts);

/// <summary>
/// The viewings going on (C3): one ffmpeg each, copying the parts out of the file as they're asked for, a few ahead,
/// nothing written to disk. A part far from where it is (a seek) starts it again from there.
/// </summary>
public interface IStreamSessions
{
    /// <summary>The init segment (the MP4 header every part needs before it), the same for every viewing of the file.</summary>
    /// <exception cref="StreamFailedException">ffmpeg couldn't copy this file into parts.</exception>
    Task<byte[]> InitAsync(StreamSource source, string session, CancellationToken cancellationToken);

    /// <exception cref="StreamFailedException">ffmpeg couldn't copy this part.</exception>
    Task<byte[]> PartAsync(StreamSource source, string session, int index, CancellationToken cancellationToken);

    /// <summary>The viewer left: its ffmpeg stops now instead of after its idle time.</summary>
    void Close(string session);
}

/// <summary>ffmpeg couldn't copy a file into parts (a codec MP4 can't hold, a damaged file…): it's played whole.</summary>
public sealed class StreamFailedException(string message, Exception? inner = null) : Exception(message, inner);
