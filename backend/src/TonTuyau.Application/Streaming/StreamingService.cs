using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using TonTuyau.Application.Browse;
using TonTuyau.Application.Media;

namespace TonTuyau.Application.Streaming;

/// <summary>
/// A video played in parts (2.0, C3, question 9): an HLS playlist of its keyframe-to-keyframe parts, each copied out of
/// the file by ffmpeg when it's asked for, a few seconds ahead, so the player doesn't fetch the whole file as fast as
/// the connection goes. The keyframe times are read from the file's own index at its first play and kept. A file
/// that can't be played in parts (no index, a codec MP4 can't hold) is a <see cref="ConflictException"/>: the player
/// plays it whole, as before.
/// </summary>
public sealed partial class StreamingService(
    MediaService media,
    IKeyframeReader reader,
    IKeyframeStore keyframes,
    IStreamSessions sessions,
    ILogger<StreamingService> logger)
{
    public const string InitName = "init.mp4";

    public const string PartExtension = ".m4s";

    /// <exception cref="ValidationException">Not a video id, or not a session id.</exception>
    /// <exception cref="NotFoundException">Unknown video, or its file isn't on the share.</exception>
    /// <exception cref="ConflictException">It can't be played in parts: play it whole.</exception>
    public async Task<string> GetPlaylistAsync(string videoId, string session, CancellationToken cancellationToken)
    {
        StreamSource source = await SourceAsync(videoId, session, cancellationToken).ConfigureAwait(false);
        return StreamPlan.Playlist(source.Parts, InitName, index => $"{index}{PartExtension}");
    }

    /// <inheritdoc cref="GetPlaylistAsync"/>
    public async Task<byte[]> GetInitAsync(string videoId, string session, CancellationToken cancellationToken)
    {
        StreamSource source = await SourceAsync(videoId, session, cancellationToken).ConfigureAwait(false);
        try
        {
            return await sessions.InitAsync(source, session, cancellationToken).ConfigureAwait(false);
        }
        catch (StreamFailedException ex)
        {
            throw NotInParts(videoId, ex);
        }
    }

    /// <inheritdoc cref="GetPlaylistAsync"/>
    /// <exception cref="NotFoundException">No such part.</exception>
    public async Task<byte[]> GetPartAsync(
        string videoId,
        string session,
        int index,
        CancellationToken cancellationToken)
    {
        StreamSource source = await SourceAsync(videoId, session, cancellationToken).ConfigureAwait(false);
        if (index < 0 || index >= source.Parts.Count)
        {
            throw new NotFoundException($"The video '{videoId}' has no part {index}.");
        }

        try
        {
            return await sessions.PartAsync(source, session, index, cancellationToken).ConfigureAwait(false);
        }
        catch (StreamFailedException ex)
        {
            throw NotInParts(videoId, ex);
        }
    }

    /// <summary>
    /// Reads and keeps a video's keyframe times now (a download just placed its file), so its first play needn't. Best
    /// effort: a file that can't be read is read at its first play.
    /// </summary>
    public async Task PrepareAsync(string videoId, CancellationToken cancellationToken)
    {
        try
        {
            MediaFile file = await media.GetVideoFileAsync(videoId, cancellationToken).ConfigureAwait(false);
            await KeyframesAsync(videoId, file, cancellationToken).ConfigureAwait(false);
        }
        catch (NotFoundException ex)
        {
            LogNotRead(ex, videoId);
        }
    }

    /// <summary>The viewer left (the page, or another video): its ffmpeg stops now.</summary>
    /// <exception cref="ValidationException">Not a session id.</exception>
    public void Close(string session)
    {
        RequireSession(session);
        sessions.Close(session);
    }

    private async Task<StreamSource> SourceAsync(string videoId, string session, CancellationToken cancellationToken)
    {
        RequireSession(session);
        MediaFile file = await media.GetVideoFileAsync(videoId, cancellationToken).ConfigureAwait(false);
        IReadOnlyList<double> times = await KeyframesAsync(videoId, file, cancellationToken).ConfigureAwait(false);
        IReadOnlyList<StreamPart> parts = StreamPlan.Parts(times, file.Duration.TotalSeconds);
        if (parts.Count == 0)
        {
            throw new ConflictException($"The file of the video '{videoId}' can't be played in parts: play it whole.");
        }

        return new StreamSource(videoId, file.File.FullPath, file.File.Length, parts);
    }

    /// <summary>Kept ones for this very file; else read from its index (first play, or a new file), then kept.</summary>
    private async Task<IReadOnlyList<double>> KeyframesAsync(
        string videoId,
        MediaFile file,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<double>? kept = await keyframes
            .GetAsync(videoId, file.File.Length, cancellationToken)
            .ConfigureAwait(false);
        if (kept is not null)
        {
            return kept;
        }

        IReadOnlyList<double> read;
        try
        {
            read = await reader.ReadAsync(file.File.FullPath, cancellationToken).ConfigureAwait(false) ?? [];
        }
        catch (IOException ex)
        {
            // Not kept: read again at the next play (the share may come back).
            LogNotRead(ex, videoId);
            return [];
        }

        await keyframes.SaveAsync(videoId, file.File.Length, read, cancellationToken).ConfigureAwait(false);
        LogRead(videoId, read.Count);
        return read;
    }

    private static ConflictException NotInParts(string videoId, StreamFailedException ex)
    {
        return new ConflictException($"The file of the video '{videoId}' can't be played in parts: {ex.Message}", ex);
    }

    private static void RequireSession(string session)
    {
        if (!SessionId().IsMatch(session))
        {
            throw RequestRules.Invalid("session", "must be 16 to 64 letters, digits, - or _.");
        }
    }

    [GeneratedRegex("^[A-Za-z0-9_-]{16,64}$")]
    private static partial Regex SessionId();

    [LoggerMessage(Level = LogLevel.Information, Message = "Keyframes of {VideoId} read: {Count}.")]
    private partial void LogRead(string videoId, int count);

    [LoggerMessage(Level = LogLevel.Warning, Message = "The keyframes of {VideoId} couldn't be read: played whole.")]
    private partial void LogNotRead(Exception exception, string videoId);
}
