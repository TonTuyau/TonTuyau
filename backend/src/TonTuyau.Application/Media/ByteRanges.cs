using System.Globalization;
using TonTuyau.Application.Sync;

namespace TonTuyau.Application.Media;

/// <summary>The part of a file to send: bytes <see cref="Start"/> to <see cref="End"/>, both included.</summary>
public sealed record ByteRange(long Start, long End)
{
    public long Length => End - Start + 1;

    /// <summary>The <c>Content-Range</c> header of a 206 answer, e.g. <c>bytes 0-99/1000</c>.</summary>
    public string ContentRange(long fileLength) =>
        string.Create(CultureInfo.InvariantCulture, $"bytes {Start}-{End}/{fileLength}");
}

/// <summary>The range asked for can't be served from this file (RFC 9110 §15.5.17): a 416.</summary>
public sealed class RangeNotSatisfiableException(long fileLength)
    : Exception($"The range asked for is outside the file ({fileLength} bytes).")
{
    public long FileLength { get; } = fileLength;

    /// <summary>The <c>Content-Range</c> header of the 416 answer, e.g. <c>bytes */1000</c>.</summary>
    public string ContentRange => string.Create(CultureInfo.InvariantCulture, $"bytes */{FileLength}");
}

/// <summary>
/// HTTP range requests (RFC 9110 §14), for seeking in videos. One range per request: <c>bytes=a-b</c>,
/// <c>bytes=a-</c> (to the end) or <c>bytes=-n</c> (the last n bytes). Anything the RFC lets a server ignore
/// (another unit, several ranges, a malformed header, a stale <c>If-Range</c>) gets the whole file.
/// </summary>
public static class ByteRanges
{
    /// <summary>A strong validator for the file: changes when its size or date changes.</summary>
    public static string ETag(StoredFile file)
    {
        ArgumentNullException.ThrowIfNull(file);
        return string.Create(
            CultureInfo.InvariantCulture,
            $"\"{file.Length:x}-{file.LastModified.UtcTicks:x}\"");
    }

    /// <param name="range">The <c>Range</c> header, if any.</param>
    /// <param name="ifRange">The <c>If-Range</c> header, if any: the range only applies to that version of the file.</param>
    /// <returns>The range to send, or null for the whole file.</returns>
    /// <exception cref="RangeNotSatisfiableException">The range starts past the end of the file.</exception>
    public static ByteRange? Select(string? range, string? ifRange, StoredFile file)
    {
        ArgumentNullException.ThrowIfNull(file);
        if (string.IsNullOrWhiteSpace(range))
        {
            return null;
        }

        return string.IsNullOrWhiteSpace(ifRange) || IsCurrent(ifRange.Trim(), file) ? Parse(range, file.Length) : null;
    }

    /// <inheritdoc cref="Select"/>
    public static ByteRange? Parse(string? header, long length)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(length);
        const string Unit = "bytes=";
        string? trimmed = header?.Trim();
        if (trimmed is null || !trimmed.StartsWith(Unit, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        string[] specs = trimmed[Unit.Length..].Split(
            ',',
            StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (specs.Length != 1)
        {
            return null;
        }

        int dash = specs[0].IndexOf('-', StringComparison.Ordinal);
        if (dash < 0)
        {
            return null;
        }

        string first = specs[0][..dash].Trim();
        string last = specs[0][(dash + 1)..].Trim();
        return first.Length == 0 ? Suffix(last, length) : FromTo(first, last, length);
    }

    /// <summary><c>bytes=-n</c>: the last n bytes (the whole file when it's shorter).</summary>
    private static ByteRange? Suffix(string count, long length)
    {
        if (!IsDigits(count))
        {
            return null;
        }

        // More digits than a long holds: certainly more than the file.
        long n = long.TryParse(count, NumberStyles.None, CultureInfo.InvariantCulture, out long parsed)
            ? parsed
            : long.MaxValue;
        if (n == 0 || length == 0)
        {
            throw new RangeNotSatisfiableException(length);
        }

        return new ByteRange(
            Math.Max(0, length - n),
            length - 1);
    }

    /// <summary><c>bytes=a-b</c> or <c>bytes=a-</c>.</summary>
    private static ByteRange? FromTo(string first, string last, long length)
    {
        if (!IsDigits(first) || (last.Length > 0 && !IsDigits(last)))
        {
            return null;
        }

        long start = long.TryParse(first, NumberStyles.None, CultureInfo.InvariantCulture, out long a)
            ? a
            : long.MaxValue;
        long end = last.Length == 0
            ? long.MaxValue
            : long.TryParse(last, NumberStyles.None, CultureInfo.InvariantCulture, out long b) ? b : long.MaxValue;
        if (end < start)
        {
            // Invalid (RFC 9110 §14.1.1): ignored, not refused.
            return null;
        }

        if (start >= length)
        {
            throw new RangeNotSatisfiableException(length);
        }

        return new ByteRange(
            start,
            Math.Min(end, length - 1));
    }

    /// <summary>
    /// <c>If-Range</c> names the version the client already has part of: an entity tag (strong comparison: a weak
    /// tag never matches) or the exact Last-Modified date.
    /// </summary>
    private static bool IsCurrent(string ifRange, StoredFile file)
    {
        if (ifRange.StartsWith('"') || ifRange.StartsWith("W/", StringComparison.Ordinal))
        {
            return string.Equals(ifRange, ETag(file), StringComparison.Ordinal);
        }

        bool isDate = DateTimeOffset.TryParseExact(
            ifRange,
            "r",
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal,
            out DateTimeOffset date);
        return isDate && date == TruncateToSeconds(file.LastModified);
    }

    /// <summary>HTTP dates have whole seconds.</summary>
    public static DateTimeOffset TruncateToSeconds(DateTimeOffset value) =>
        new(value.UtcTicks - (value.UtcTicks % TimeSpan.TicksPerSecond), TimeSpan.Zero);

    private static bool IsDigits(string text) => text.Length > 0 && text.All(char.IsAsciiDigit);
}
