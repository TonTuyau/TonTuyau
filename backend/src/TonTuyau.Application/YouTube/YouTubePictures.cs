namespace TonTuyau.Application.YouTube;

/// <summary>Which of the pictures YouTube offers a refresh keeps (1.5), and where they go in the art volume.</summary>
public static class YouTubePictures
{
    /// <summary>A video's largest thumbnail.</summary>
    public static string? Thumbnail(IReadOnlyList<YouTubeThumbnail> thumbnails) =>
        thumbnails
            .OrderByDescending(t => t.Width ?? 0)
            .FirstOrDefault()
            ?.Url;

    /// <summary>A channel's avatar, uncropped (yt-dlp's <c>avatar_uncropped</c>).</summary>
    public static string? Avatar(IReadOnlyList<YouTubeThumbnail> thumbnails) =>
        thumbnails.FirstOrDefault(t => t.Id == "avatar_uncropped")?.Url;

    /// <summary>A channel's banner, uncropped (yt-dlp's <c>banner_uncropped</c>).</summary>
    public static string? Banner(IReadOnlyList<YouTubeThumbnail> thumbnails) =>
        thumbnails.FirstOrDefault(t => t.Id == "banner_uncropped")?.Url;

    /// <summary>
    /// Where a picture goes: where the current one is when it's the same format (it replaces it), else beside it,
    /// named like TubeArchivist names its own (<paramref name="stem"/> and the picture's format).
    /// </summary>
    /// <param name="current">The current picture's art path, if any.</param>
    /// <param name="stem">E.g. <c>/cache/videos/k/k3Fq8xZ0aQw</c>, <c>/cache/channels/UC…_thumb</c>.</param>
    public static string PathFor(string? current, string stem, string extension) =>
        current is not null && SameFormat(Path.GetExtension(current), extension) ? current : $"{stem}.{extension}";

    public static string VideoStem(string videoId) => $"/cache/videos/{videoId[0]}/{videoId}";

    public static string ChannelStem(string channelId, string kind) => $"/cache/channels/{channelId}_{kind}";

    private static bool SameFormat(string currentExtension, string extension) =>
        currentExtension.TrimStart('.') switch
        {
            "jpg" or "jpeg" => extension == "jpg",
            string other => other == extension,
        };
}
