using TonTuyau.Application.Browse;
using TonTuyau.Application.Validation;

namespace TonTuyau.Application.YouTube;

/// <summary>The YouTube settings of the admin's YouTube page (1.5): reading them, changing them.</summary>
public sealed class YouTubeSettingsService(IYouTubeSettingsRepository settings, TimeProvider clock)
{
    public Task<YouTubeSettings> GetAsync(CancellationToken cancellationToken) =>
        settings.GetAsync(cancellationToken);

    /// <param name="readComments">Whether refreshes read the videos' comments; required.</param>
    /// <param name="by">The admin changing them.</param>
    /// <exception cref="ValidationException">A setting is missing.</exception>
    public async Task<YouTubeSettings> SaveAsync(bool? readComments, string by, CancellationToken cancellationToken)
    {
        if (readComments is null)
        {
            throw RequestRules.Invalid("readComments", "is required: true or false.");
        }

        YouTubeSettings changed = new(readComments.Value, by, clock.GetUtcNow());
        await settings.SaveAsync(changed, cancellationToken).ConfigureAwait(false);
        return changed;
    }
}
