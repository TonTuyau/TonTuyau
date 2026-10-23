using TonTuyau.Application.Browse;
using TonTuyau.Application.Library;
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
    /// <param name="syncEveryMinutes">
    /// How often the incremental YouTube sync runs by itself, in minutes (2.1.0; 1 to a week, 0: by hand only); absent:
    /// the one kept.
    /// </param>
    /// <param name="downloadFormat">
    /// What a download asks yt-dlp for (2.0, B6), in its syntax: one line, not empty, 200 characters at most (checked no
    /// further: a format that picks nothing fails the download, saying why); absent: the one kept.
    /// </param>
    /// <param name="defaultTabs">The tabs a channel added gets when none is named (B6), at least one; absent: kept.</param>
    /// <param name="by">The admin changing them.</param>
    /// <exception cref="ValidationException">A setting is missing or out of bounds.</exception>
    public async Task<YouTubeSettings> SaveAsync(
        string? comments,
        int? commentsMax,
        int? pauseSeconds,
        int? holdMinutes,
        int? syncEveryMinutes,
        string? downloadFormat,
        ChannelTabs? defaultTabs,
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

        if (syncEveryMinutes is < 0 or > YouTubeSettings.MaxSyncEveryMinutes)
        {
            errors.Add(
                "syncEveryMinutes",
                $"must be 0 (by hand only) to {YouTubeSettings.MaxSyncEveryMinutes} (a week).");
        }

        string? format = downloadFormat?.Trim();
        if (format is not null
            && (format.Length == 0
                || format.Length > YouTubeSettings.MaxDownloadFormatLength
                || format.Any(char.IsControl)))
        {
            errors.Add(
                "downloadFormat",
                $"must be yt-dlp's format, on one line, 1 to {YouTubeSettings.MaxDownloadFormatLength} characters.");
        }

        if (defaultTabs is { Any: false })
        {
            errors.Add("defaultTabs", "at least one tab: videos, shorts or streams.");
        }

        errors.ThrowIfAny(RequestRules.Subject);
        YouTubeSettings current = await settings.GetAsync(cancellationToken).ConfigureAwait(false);
        // What isn't given stays as it is.
        YouTubeSettings changed = current with
        {
            Comments = ApiValues.CommentChoices[comments!],
            CommentsMax = commentsMax ?? current.CommentsMax,
            PauseSeconds = pauseSeconds ?? current.PauseSeconds,
            HoldMinutes = holdMinutes ?? current.HoldMinutes,
            SyncEveryMinutes = syncEveryMinutes ?? current.SyncEveryMinutes,
            DownloadFormat = format ?? current.DownloadFormat,
            DefaultTabs = defaultTabs ?? current.DefaultTabs,
            ChangedBy = by,
            ChangedAt = clock.GetUtcNow(),
        };
        await settings.SaveAsync(changed, cancellationToken).ConfigureAwait(false);
        return changed;
    }
}
