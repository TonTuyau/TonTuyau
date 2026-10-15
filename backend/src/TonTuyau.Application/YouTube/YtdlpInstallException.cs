namespace TonTuyau.Application.YouTube;

/// <summary>A yt-dlp version couldn't be listed or installed: the message says why, for the admin.</summary>
public sealed class YtdlpInstallException(string message, Exception? inner = null) : Exception(message, inner);
