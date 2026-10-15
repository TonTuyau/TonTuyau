using TonTuyau.Application.Browse;
using TonTuyau.Application.Validation;

namespace TonTuyau.Application.YouTube;

/// <summary>The YouTube settings of the admin's YouTube page (1.5): reading them, changing them.</summary>
public sealed class YouTubeSettingsService(IYouTubeSettingsRepository settings, TimeProvider clock)
{
    public Task<YouTubeSettings> GetAsync(CancellationToken cancellationToken) =>
        settings.GetAsync(cancellationToken);

    /// <param name="comments"><c>off</c>, <c>top</c> or <c>all</c>; required.</param>
    /// <param name="commentsMax">For <c>top</c>: how many (1 to 100,000); absent: the one kept.</param>
    /// <param name="by">The admin changing them.</param>
    /// <exception cref="ValidationException">A setting is missing or out of bounds.</exception>
    public async Task<YouTubeSettings> SaveAsync(
        string? comments,
        int? commentsMax,
        string by,
        CancellationToken cancellationToken)
    {
        ValidationErrors errors = new();
        if (comments is null)
        {
            errors.Add("comments", $"is required: one of {ApiValues.CommentChoices.Choices}.");
        }

        RequestRules.OneOf(errors, "comments", comments, ApiValues.CommentChoices);
        if (commentsMax is < 1 or > YouTubeSettings.MaxCommentsMax)
        {
            errors.Add("commentsMax", $"must be from 1 to {YouTubeSettings.MaxCommentsMax}.");
        }

        errors.ThrowIfAny(RequestRules.Subject);
        YouTubeSettings current = await settings.GetAsync(cancellationToken).ConfigureAwait(false);
        YouTubeSettings changed = new(
            ApiValues.CommentChoices[comments!],
            commentsMax ?? current.CommentsMax,
            by,
            clock.GetUtcNow());
        await settings.SaveAsync(changed, cancellationToken).ConfigureAwait(false);
        return changed;
    }
}
