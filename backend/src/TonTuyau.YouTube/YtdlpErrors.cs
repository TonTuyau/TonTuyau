using TonTuyau.Application.YouTube;

namespace TonTuyau.YouTube;

/// <summary>
/// Reads yt-dlp's <c>ERROR:</c> lines (stderr) into an answer: <see cref="YouTubeAnswer.Gone"/> only when YouTube itself
/// says the video or channel was removed or its account terminated, <see cref="YouTubeAnswer.Private"/> when it says
/// it's private; anything else (a bot check, too many
/// requests, a country block, members only, an unknown "unavailable", no error at all) is
/// <see cref="YouTubeAnswer.CheckFailed"/>: TubeArchivist's false "unavailable" flags came from treating those as gone.
/// A bot check or "too many requests" is <see cref="YouTubeAnswer.Refused"/>: YouTube refuses TonTuyau for now.
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

    /// <summary>YouTube refusing TonTuyau itself, whatever the video: these win over everything else.</summary>
    private static readonly string[] Refusals =
    [
        "confirm you’re not a bot",
        "confirm you're not a bot",
        "HTTP Error 429",
        "Too Many Requests",
    ];

    /// <summary>YouTube's own words for a video or channel that's gone.</summary>
    private static readonly string[] GoneWords =
    [
        "removed by the uploader",
        "has been removed",
        "account associated with this video has been terminated",
        "This video is no longer available",
        "violating YouTube's Terms of Service",
        "violating YouTube’s Terms of Service",
        "copyright claim",
        "This channel does not exist",
        "This account has been terminated",
    ];

    /// <summary>YouTube's own words for a video its owner made private.</summary>
    private static readonly string[] PrivateWords =
    [
        "Private video",
        "This video is private",
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
        if (Refusals.Any(w => reason.Contains(w, StringComparison.OrdinalIgnoreCase)))
        {
            return (YouTubeAnswer.Refused, reason);
        }

        if (DidntAnswer(reason))
        {
            return (YouTubeAnswer.CheckFailed, reason);
        }

        if (GoneWords.Any(w => reason.Contains(w, StringComparison.OrdinalIgnoreCase)))
        {
            return (YouTubeAnswer.Gone, reason);
        }

        return PrivateWords.Any(w => reason.Contains(w, StringComparison.OrdinalIgnoreCase))
            ? (YouTubeAnswer.Private, reason)
            : (YouTubeAnswer.CheckFailed, reason);
    }

    /// <summary>The error lines alone, without their prefix; empty when yt-dlp wrote none.</summary>
    public static string ReasonOf(IReadOnlyList<string> lines)
    {
        ArgumentNullException.ThrowIfNull(lines);
        return string.Join(
            " ",
            lines
                .Where(l => l.StartsWith("ERROR:", StringComparison.Ordinal))
                .Select(Clean));
    }

    /// <summary>YouTube didn't really answer (a bot check, too many requests, the network…): nothing to conclude.</summary>
    public static bool DidntAnswer(string reason) =>
        NotAnAnswer.Any(w => reason.Contains(w, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// The format asked for matched none of the video's (bestvideo, a height, a codec YouTube doesn't give); signed in,
    /// YouTube can give yt-dlp a single format.
    /// </summary>
    public static bool FormatUnavailable(string reason) =>
        reason.Contains("Requested format is not available", StringComparison.OrdinalIgnoreCase);

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
