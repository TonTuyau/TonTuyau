namespace TonTuyau.Application.YouTube;

/// <summary>The yt-dlp the admin chose (1.5); <paramref name="Version"/> unless <see cref="YtdlpChoiceKind.Latest"/>.</summary>
/// <param name="ChosenBy">The account; null for the default (nobody chose yet).</param>
public sealed record YtdlpChoice(YtdlpChoiceKind Kind, string? Version, string? ChosenBy, DateTimeOffset? ChosenAt)
{
    public static readonly YtdlpChoice Default = new(YtdlpChoiceKind.Latest, null, null, null);
}
