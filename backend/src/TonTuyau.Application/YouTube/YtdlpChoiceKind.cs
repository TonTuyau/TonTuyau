namespace TonTuyau.Application.YouTube;

/// <summary>Which yt-dlp the admin wants (1.5).</summary>
public enum YtdlpChoiceKind
{
    /// <summary>The latest official release, kept up to date (checked daily): the default.</summary>
    Latest,

    /// <summary>One official release, kept.</summary>
    Official,

    /// <summary>One nightly build, kept.</summary>
    Nightly,
}
