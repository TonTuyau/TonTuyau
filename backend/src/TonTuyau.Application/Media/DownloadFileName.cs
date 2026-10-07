using System.Globalization;
using System.Text;

namespace TonTuyau.Application.Media;

/// <param name="Ascii">For the plain <c>filename=</c> parameter (old clients): accents dropped, the rest replaced.</param>
/// <param name="Unicode">The real name, for <c>filename*=UTF-8''…</c> (every current browser uses this one).</param>
public sealed record DownloadName(string Ascii, string Unicode)
{
    /// <summary>
    /// <c>attachment; filename="&lt;ascii&gt;"; filename*=UTF-8''&lt;percent-encoded unicode&gt;</c> (RFC 6266, RFC 8187).
    /// </summary>
    public string ContentDisposition =>
        $"attachment; filename=\"{Ascii}\"; filename*=UTF-8''{Uri.EscapeDataString(Unicode)}";
}

/// <summary>
/// The name a downloaded video is saved under: its real title (Q5-3), made safe for Windows, macOS and Linux file
/// systems. Characters a file name can't hold are replaced with <c>_</c>.
/// </summary>
public static class DownloadFileName
{
    /// <summary>In text elements (what a person sees as one character): keeps the name well under 255 bytes.</summary>
    public const int MaxTitleLength = 150;

    public const string Fallback = "video";

    private const string Forbidden = "\\/:*?\"<>|";

    private static readonly HashSet<string> ReservedOnWindows = new(
        [
            "CON",
            "PRN",
            "AUX",
            "NUL",
            .. Enumerable
                .Range(1, 9)
                .SelectMany(i => (string[])[$"COM{i}", $"LPT{i}"]),
        ],
        StringComparer.OrdinalIgnoreCase);

    /// <summary>ASCII stand-ins for the non-ASCII characters French and English titles use most.</summary>
    private static readonly Dictionary<char, string> AsciiStandIns = new()
    {
        ['œ'] = "oe",
        ['Œ'] = "OE",
        ['æ'] = "ae",
        ['Æ'] = "AE",
        ['ß'] = "ss",
        ['’'] = "'",
        ['‘'] = "'",
        ['‚'] = "'",
        ['–'] = "-",
        ['—'] = "-",
        ['…'] = "...",
    };

    /// <param name="extension">Without the dot, e.g. <c>mp4</c>.</param>
    public static DownloadName For(string title, string extension)
    {
        ArgumentNullException.ThrowIfNull(title);
        ArgumentNullException.ThrowIfNull(extension);
        string name = SafeTitle(title);
        string ascii = SafeTitle(ToAscii(name));
        return new DownloadName($"{ascii}.{extension}", $"{name}.{extension}");
    }

    /// <summary>The title as a file name (without extension): never empty, never a reserved name.</summary>
    public static string SafeTitle(string title)
    {
        ArgumentNullException.ThrowIfNull(title);
        StringBuilder safe = new(title.Length);
        foreach (char c in title)
        {
            safe.Append(c switch
            {
                _ when char.IsControl(c) || char.IsWhiteSpace(c) => ' ',
                _ when Forbidden.Contains(c, StringComparison.Ordinal) => '_',
                _ => c,
            });
        }

        string name = Trim(CollapseSpaces(safe.ToString()));
        StringInfo elements = new(name);
        if (elements.LengthInTextElements > MaxTitleLength)
        {
            name = Trim(elements.SubstringByTextElements(0, MaxTitleLength));
        }

        if (name.Length == 0)
        {
            return Fallback;
        }

        // "CON.mp4" can't be created on Windows, whatever the extension.
        return ReservedOnWindows.Contains(name) ? name + "_" : name;
    }

    private static string ToAscii(string name)
    {
        StringBuilder ascii = new(name.Length);
        IEnumerable<Rune> runes = name
            .Normalize(NormalizationForm.FormD)
            .EnumerateRunes();
        foreach (Rune rune in runes)
        {
            if (rune.IsAscii)
            {
                // '%' could be read as percent-encoding by old clients.
                ascii.Append(rune.Value == '%' ? '_' : (char)rune.Value);
            }
            else if (rune.IsBmp && AsciiStandIns.TryGetValue((char)rune.Value, out string? standIn))
            {
                ascii.Append(standIn);
            }
            else if (Rune.GetUnicodeCategory(rune) != UnicodeCategory.NonSpacingMark)
            {
                // Accents were split off by the decomposition and are dropped; anything else has no ASCII form.
                ascii.Append('_');
            }
        }

        return ascii.ToString();
    }

    private static string CollapseSpaces(string text)
    {
        StringBuilder collapsed = new(text.Length);
        foreach (char c in text)
        {
            if (c != ' ' || collapsed.Length == 0 || collapsed[^1] != ' ')
            {
                collapsed.Append(c);
            }
        }

        return collapsed.ToString();
    }

    /// <summary>Windows drops trailing dots and spaces; leading spaces are just noise.</summary>
    private static string Trim(string name) =>
        name
            .Trim(' ')
            .TrimEnd('.', ' ');
}
