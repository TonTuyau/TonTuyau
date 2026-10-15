using Microsoft.Extensions.Logging;
using TonTuyau.Application.Browse;
using TonTuyau.Application.Sync;
using TonTuyau.Application.Validation;

namespace TonTuyau.Application.YouTube;

/// <summary>
/// Which yt-dlp TonTuyau runs (1.5): by default the latest official release, kept up to date; or one official release,
/// or one nightly build of the last 2 months, as the admin chooses. A version is downloaded from its GitHub release,
/// checked and tried before use (<see cref="IYtdlpInstaller"/>), and switched to between tasks only: while a run goes
/// on, it waits. The image's copy is the start, and the fallback when nothing else can be had.
/// </summary>
public sealed partial class YtdlpService(
    IYtdlpSettingsRepository settings,
    IYtdlpReleases releases,
    IYtdlpInstaller installer,
    YtdlpLocator locator,
    YtdlpUpdateSignal signal,
    SyncRunner runner,
    TimeProvider clock,
    ILogger<YtdlpService> logger)
{
    /// <summary>How far back the nightly builds offered go: two months.</summary>
    public static readonly TimeSpan NightlyWindow = TimeSpan.FromDays(61);

    public async Task<YtdlpState> GetStateAsync(CancellationToken cancellationToken)
    {
        YtdlpSettings stored = await settings
            .GetAsync(cancellationToken)
            .ConfigureAwait(false);
        return new YtdlpState(
            stored.Choice,
            locator.Version,
            locator.Bundled,
            locator.Waiting,
            stored.CheckedAt,
            stored.Error);
    }

    /// <exception cref="YtdlpInstallException">GitHub can't be reached.</exception>
    public async Task<YtdlpVersions> ListVersionsAsync(CancellationToken cancellationToken)
    {
        IReadOnlyList<YtdlpRelease> official = await releases
            .ListAsync(YtdlpChannel.Official, cancellationToken)
            .ConfigureAwait(false);
        IReadOnlyList<YtdlpRelease> nightly = await releases
            .ListAsync(YtdlpChannel.Nightly, cancellationToken)
            .ConfigureAwait(false);
        DateTimeOffset since = clock.GetUtcNow() - NightlyWindow;
        return new YtdlpVersions(official, [.. nightly.Where(r => r.PublishedAt >= since)]);
    }

    /// <summary>Stores the admin's choice, and has the updater apply it now.</summary>
    /// <exception cref="ValidationException">The kind is unknown, or the version isn't one that can be chosen.</exception>
    /// <exception cref="YtdlpInstallException">GitHub can't be reached (to check the version).</exception>
    public async Task<YtdlpState> ChooseAsync(
        string? kind,
        string? version,
        string by,
        CancellationToken cancellationToken)
    {
        ValidationErrors errors = new();
        if (kind is null)
        {
            errors.Add("kind", $"is required: one of {ApiValues.YtdlpChoices.Choices}.");
        }

        RequestRules.OneOf(errors, "kind", kind, ApiValues.YtdlpChoices);
        errors.ThrowIfAny(RequestRules.Subject);
        YtdlpChoiceKind choiceKind = ApiValues.YtdlpChoices[kind!];
        if (choiceKind == YtdlpChoiceKind.Latest)
        {
            version = null;
        }
        else
        {
            YtdlpVersions offered = await ListVersionsAsync(cancellationToken).ConfigureAwait(false);
            IReadOnlyList<YtdlpRelease> list = choiceKind == YtdlpChoiceKind.Official
                ? offered.Official
                : offered.Nightly;
            if (!list.Any(r => r.Version == version))
            {
                errors.Add("version", $"isn't a {kind} version that can be chosen (see the versions offered).");
            }

            errors.ThrowIfAny(RequestRules.Subject);
        }

        await settings
            .SaveChoiceAsync(new YtdlpChoice(choiceKind, version, by, clock.GetUtcNow()), cancellationToken)
            .ConfigureAwait(false);
        LogChosen(choiceKind, version ?? "latest", by);
        signal.Wake();
        return await GetStateAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Makes the chosen version the one running: found on GitHub (the latest, for the default), installed if it isn't,
    /// switched to unless a run goes on. Called by the updater only (at startup, daily, when the choice changes).
    /// </summary>
    public async Task<YtdlpApplyResult> ApplyAsync(CancellationToken cancellationToken)
    {
        YtdlpSettings stored = await settings
            .GetAsync(cancellationToken)
            .ConfigureAwait(false);
        try
        {
            YtdlpRelease target = await TargetAsync(stored.Choice, cancellationToken).ConfigureAwait(false);
            YtdlpApplyResult result = await SwitchToAsync(target, cancellationToken).ConfigureAwait(false);
            await settings
                .SaveCheckAsync(clock.GetUtcNow(), null, cancellationToken)
                .ConfigureAwait(false);
            return result;
        }
        catch (YtdlpInstallException ex)
        {
            LogFailed(ex.Message);
            await settings
                .SaveCheckAsync(clock.GetUtcNow(), ex.Message, cancellationToken)
                .ConfigureAwait(false);
            return YtdlpApplyResult.Failed;
        }
    }

    private async Task<YtdlpRelease> TargetAsync(YtdlpChoice choice, CancellationToken cancellationToken)
    {
        YtdlpChannel channel = choice.Kind == YtdlpChoiceKind.Nightly ? YtdlpChannel.Nightly : YtdlpChannel.Official;
        IReadOnlyList<YtdlpRelease> list = await releases
            .ListAsync(channel, cancellationToken)
            .ConfigureAwait(false);
        YtdlpRelease? target = choice.Kind == YtdlpChoiceKind.Latest
            ? (list.Count > 0 ? list[0] : null)
            : list.FirstOrDefault(r => r.Version == choice.Version);
        return target ?? throw new YtdlpInstallException(
            choice.Kind == YtdlpChoiceKind.Latest
                ? "GitHub lists no official yt-dlp release for Linux."
                : $"yt-dlp {choice.Version} isn't on GitHub anymore.");
    }

    private async Task<YtdlpApplyResult> SwitchToAsync(YtdlpRelease target, CancellationToken cancellationToken)
    {
        if (locator.Version == target.Version)
        {
            locator.Waiting = null;
            return YtdlpApplyResult.UpToDate;
        }

        bool bundled = target.Version == locator.BundledVersion;
        if (!bundled && !installer.IsInstalled(target.Version))
        {
            await installer
                .InstallAsync(target, cancellationToken)
                .ConfigureAwait(false);
            LogInstalled(target.Version);
        }

        if (runner.RunningId is not null)
        {
            locator.Waiting = target.Version;
            return YtdlpApplyResult.WaitingForTask;
        }

        locator.Switch(bundled ? locator.BundledPath : installer.PathOf(target.Version), target.Version);
        installer.SetActive(bundled ? "" : target.Version);
        LogSwitched(target.Version, bundled ? "the image's copy" : "downloaded");
        return YtdlpApplyResult.UpToDate;
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "yt-dlp: {Kind} {Version} chosen by {By}.")]
    private partial void LogChosen(YtdlpChoiceKind kind, string version, string by);

    [LoggerMessage(Level = LogLevel.Information, Message = "yt-dlp {Version} installed (checksum and try passed).")]
    private partial void LogInstalled(string version);

    [LoggerMessage(Level = LogLevel.Information, Message = "yt-dlp: now {Version} ({Where}).")]
    private partial void LogSwitched(string version, string where);

    [LoggerMessage(Level = LogLevel.Warning, Message = "yt-dlp: the version chosen can't be had: {Reason}")]
    private partial void LogFailed(string reason);
}
