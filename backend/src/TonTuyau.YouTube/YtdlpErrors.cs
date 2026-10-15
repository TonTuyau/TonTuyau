using TonTuyau.Application.YouTube;

namespace TonTuyau.YouTube;

/// <summary>
/// Reads yt-dlp's <c>ERROR:</c> lines (stderr) into an answer: <see cref="YouTubeAnswer.Gone"/> only when YouTube itself
/// says the video or channel was removed, made private, or its account terminated; anything else (a bot check, too many
/// requests, a country block, members only, an unknown "unavailable", no error at all) is
/// <see cref="YouTubeAnswer.CheckFailed"/>: TubeArchivist's false "unavailable" flags came from treating those as gone.
/// </summary>
internal static class YtdlpErrors
{
    /// <summary>YouTube didn't really answer: these win over the gone ones ("Sign in…" can follow "Private video").</summary>
    private static readonly string[] NotAnAnswer =
    [
        "confirm you’re not a bot",
        "confirm you're not a bot",
        "HTTP Error 429",
        "Too Many Requests",
        "in your country",
        "Sign in to confirm your age",
        "Join this channel",
        "members-only",
        "This live event will begin",
        "Premieres in",
        "Unable to download",
        "timed out",
    ];

    /// <summary>YouTube's own words for a video or channel that's gone.</summary>
    private static readonly string[] GoneWords =
    [
        "removed by the uploader",
        "has been removed",
        "account associated with this video has been terminated",
        "This video is no longer available",
        "Private video",
        "This video is private",
        "violating YouTube's Terms of Service",
        "violating YouTube’s Terms of Service",
        "copyright claim",
        "This channel does not exist",
        "This account has been terminated",
    ];

    /// <param name="lines">What yt-dlp wrote to stderr, warnings included.</param>
    /// <param name="exitCode">yt-dlp's exit code, for the reason when it wrote no error.</param>
    public static (YouTubeAnswer Answer, string Reason) Classify(IReadOnlyList<string> lines, int exitCode)
    {
        ArgumentNullException.ThrowIfNull(lines);
        string[] errors = lines
            .Where(l => l.StartsWith("ERROR:", StringComparison.Ordinal))
            .Select(Clean)
            .ToArray();
        if (errors.Length == 0)
        {
            return (YouTubeAnswer.CheckFailed, $"yt-dlp failed without saying why (exit code {exitCode}).");
        }

        string reason = string.Join(" ", errors);
        if (NotAnAnswer.Any(w => reason.Contains(w, StringComparison.OrdinalIgnoreCase)))
        {
            return (YouTubeAnswer.CheckFailed, reason);
        }

        return GoneWords.Any(w => reason.Contains(w, StringComparison.OrdinalIgnoreCase))
            ? (YouTubeAnswer.Gone, reason)
            : (YouTubeAnswer.CheckFailed, reason);
    }

    /// <summary>"ERROR: [youtube] k3Fq8xZ0aQw: Private video. …" → "Private video. …".</summary>
    private static string Clean(string line)
    {
        string text = line["ERROR:".Length..].Trim();
        if (text.StartsWith('['))
        {
            int bracket = text.IndexOf(']', StringComparison.Ordinal);
            text = bracket < 0 ? text : text[(bracket + 1)..].Trim();
            int colon = text.IndexOf(": ", StringComparison.Ordinal);
            // The id yt-dlp names before the message (an 11-character video id, a 24-character channel id, a URL).
            if (colon > 0 && !text[..colon].Contains(' ', StringComparison.Ordinal))
            {
                text = text[(colon + 2)..];
            }
        }

        return text;
    }
}
