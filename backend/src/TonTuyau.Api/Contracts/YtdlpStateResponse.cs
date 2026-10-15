using TonTuyau.Application.YouTube;

namespace TonTuyau.Api.Contracts;

/// <summary>The yt-dlp panel of the admin's YouTube page (1.5).</summary>
/// <param name="Running">The version running now; null when yt-dlp can't be run.</param>
/// <param name="Bundled">Whether that's the copy in the image (else one downloaded).</param>
/// <param name="Waiting">A version ready, waiting for the task going on to end; null when none.</param>
/// <param name="CheckedAt">When the choice was last applied (and GitHub asked, for the latest).</param>
/// <param name="Error">Why the last check or install failed; null when it went well.</param>
public sealed record YtdlpStateResponse(
    YtdlpChoiceResponse Choice,
    string? Running,
    bool Bundled,
    string? Waiting,
    DateTime? CheckedAt,
    string? Error)
{
    public static explicit operator YtdlpStateResponse(YtdlpState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        YtdlpChoice choice = state.Choice;
        return new YtdlpStateResponse(
            new YtdlpChoiceResponse(choice.Kind, choice.Version, choice.ChosenBy, choice.ChosenAt?.UtcDateTime),
            state.Running,
            state.Bundled,
            state.Waiting,
            state.CheckedAt?.UtcDateTime,
            state.Error);
    }
}
