using TonTuyau.Application.Browse;
using TonTuyau.Application.Validation;

namespace TonTuyau.Application.YouTube;

/// <summary>The YouTube settings of the admin's YouTube page (1.5): reading them, changing them.</summary>
public sealed class YouTubeSettingsService(IYouTubeSettingsRepository settings, TimeProvider clock)
{
    public Task<YouTubeSettings> GetAsync(CancellationToken cancellationToken) =>
        settings.GetAsync(cancellationToken);

    /// <param name="comments"><c>off</c>, <c>top</c> or <c>all</c>; required.</param>
    /// <param name="commentsMax">For <c>top</c>: how many (1 or more); absent: the one kept.</param>
    /// <param name="pauseSeconds">Between two videos of a refresh (0 or more); absent: the one kept.</param>
    /// <param name="holdMinutes">How long a refused refresh waits (1 or more); absent: the one kept.</param>
    /// <param name="by">The admin changing them.</param>
    /// <exception cref="ValidationException">A setting is missing or out of bounds.</exception>
    public async Task<YouTubeSettings> SaveAsync(
        string? comments,
        int? commentsMax,
        int? pauseSeconds,
        int? holdMinutes,
        string by,
        CancellationToken cancellationToken)
    {
        ValidationErrors errors = new();
        if (comments is null)
        {
            errors.Add("comments", $"is required: one of {ApiValues.CommentChoices.Choices}.");
        }

        RequestRules.OneOf(errors, "comments", comments, ApiValues.CommentChoices);
        if (commentsMax is < 1)
        {
            errors.Add("commentsMax", "must be 1 or more.");
        }

        if (pauseSeconds is < 0)
        {
            errors.Add("pauseSeconds", "must be 0 or more.");
        }

        if (holdMinutes is < 1)
        {
            errors.Add("holdMinutes", "must be 1 or more.");
        }

        errors.ThrowIfAny(RequestRules.Subject);
        YouTubeSettings current = await settings.GetAsync(cancellationToken).ConfigureAwait(false);
        YouTubeSettings changed = new(
            ApiValues.CommentChoices[comments!],
            commentsMax ?? current.CommentsMax,
            pauseSeconds ?? current.PauseSeconds,
            holdMinutes ?? current.HoldMinutes,
            by,
            clock.GetUtcNow());
        await settings.SaveAsync(changed, cancellationToken).ConfigureAwait(false);
        return changed;
    }
}
