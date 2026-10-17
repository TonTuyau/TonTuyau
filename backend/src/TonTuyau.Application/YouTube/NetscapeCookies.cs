using TonTuyau.Application.Browse;
using TonTuyau.Application.Validation;

namespace TonTuyau.Application.YouTube;

/// <summary>
/// A Netscape cookie file (what browsers export and yt-dlp reads: one cookie a line, 7 fields separated by tabs), kept
/// to its <c>youtube.com</c> cookies: those are all yt-dlp needs to sign in, and nothing else of the browser is kept.
/// </summary>
public static class NetscapeCookies
{
    public const string Header = "# Netscape HTTP Cookie File";

    /// <summary>Far more than a YouTube session's cookies: a bound on what's taken.</summary>
    public const int MaxLength = 128 * 1024;

    /// <summary>A cookie only sent over HTTP (not to scripts) is written with this before its domain.</summary>
    private const string HttpOnlyPrefix = "#HttpOnly_";

    /// <summary>The file with only its <c>youtube.com</c> cookies, under the header.</summary>
    /// <exception cref="ValidationException">Empty, too long, or without a single <c>youtube.com</c> cookie.</exception>
    public static string KeepYouTube(string? text)
    {
        ValidationErrors errors = new();
        if (string.IsNullOrWhiteSpace(text))
        {
            errors.Add("cookies", "is required: a Netscape cookie file.");
        }
        else if (text.Length > MaxLength)
        {
            errors.Add("cookies", $"must be at most {MaxLength} characters.");
        }

        errors.ThrowIfAny(RequestRules.Subject);
        string[] kept = text!
            .Split('\n')
            .Select(l => l.TrimEnd('\r'))
            .Where(IsYouTubeCookie)
            .ToArray();
        if (kept.Length == 0)
        {
            errors.Add("cookies", "holds no youtube.com cookie (a Netscape cookie file: 7 fields a line, by tabs).");
            errors.ThrowIfAny(RequestRules.Subject);
        }

        return string.Join('\n', [Header, .. kept, ""]);
    }

    private static bool IsYouTubeCookie(string line)
    {
        string[] fields = line.Split('\t');
        if (fields.Length != 7 || fields.Any(f => f.Contains('\n', StringComparison.Ordinal)))
        {
            return false;
        }

        string domain = fields[0].StartsWith(HttpOnlyPrefix, StringComparison.Ordinal)
            ? fields[0][HttpOnlyPrefix.Length..]
            : fields[0];
        if (domain.StartsWith('#'))
        {
            return false;
        }

        string host = domain.TrimStart('.');
        return host.Equals("youtube.com", StringComparison.OrdinalIgnoreCase)
            || host.EndsWith(".youtube.com", StringComparison.OrdinalIgnoreCase);
    }
}
