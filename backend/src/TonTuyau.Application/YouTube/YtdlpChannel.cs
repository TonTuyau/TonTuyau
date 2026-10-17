namespace TonTuyau.Application.YouTube;

/// <summary>Where a yt-dlp version comes from (1.5).</summary>
public enum YtdlpChannel
{
    /// <summary>yt-dlp's releases (<c>yt-dlp/yt-dlp</c>): tested, every few weeks.</summary>
    Official,

    /// <summary>yt-dlp's nightly builds (<c>yt-dlp/yt-dlp-nightly-builds</c>): YouTube fixes first, less tested.</summary>
    Nightly,
}
