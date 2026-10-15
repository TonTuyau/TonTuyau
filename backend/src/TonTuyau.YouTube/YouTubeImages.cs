using Microsoft.Extensions.Logging;
using TonTuyau.Application.YouTube;

namespace TonTuyau.YouTube;

/// <summary>
/// Downloads YouTube's pictures (1.5), over HTTPS from YouTube's picture hosts only (an address yt-dlp read on YouTube,
/// never followed elsewhere), at most <see cref="MaxBytes"/>; the format is read from the content, not trusted from
/// the address.
/// </summary>
internal sealed partial class YouTubeImages(HttpClient http, ILogger<YouTubeImages> logger) : IYouTubeImages
{
    public const int MaxBytes = 10 * 1024 * 1024;

    private static readonly string[] Hosts = ["i.ytimg.com", "yt3.googleusercontent.com", "yt3.ggpht.com"];

    public async Task<YouTubeImage?> GetAsync(string url, CancellationToken cancellationToken)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out Uri? address)
            || address.Scheme != Uri.UriSchemeHttps
            || !Hosts.Contains(address.Host, StringComparer.OrdinalIgnoreCase))
        {
            LogRefused(url);
            return null;
        }

        try
        {
            using HttpRequestMessage request = new(HttpMethod.Get, address);
            // YouTube serves JPEG to whoever asks for it, else often WebP: either is kept, as it comes.
            request.Headers.Accept.ParseAdd("image/jpeg");
            using HttpResponseMessage response = await http
                .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                .ConfigureAwait(false);
            if (!response.IsSuccessStatusCode || response.Content.Headers.ContentLength > MaxBytes)
            {
                long? length = response.Content.Headers.ContentLength;
                LogFailed(address, $"answered {(int)response.StatusCode}, {length} bytes");
                return null;
            }

            byte[] content = await ReadAsync(response, cancellationToken).ConfigureAwait(false);
            string? extension = FormatOf(content);
            if (extension is null)
            {
                LogFailed(address, "not a JPEG, PNG or WebP picture, or more than 10 MB");
                return null;
            }

            return new YouTubeImage(content, extension);
        }
        catch (Exception ex) when (ex is HttpRequestException
            || ex is OperationCanceledException && !cancellationToken.IsCancellationRequested)
        {
            LogFailed(address, ex.Message);
            return null;
        }
    }

    /// <summary>jpg, png or webp, from the file's first bytes; null for anything else.</summary>
    internal static string? FormatOf(ReadOnlySpan<byte> content) => content switch
    {
        [0xFF, 0xD8, 0xFF, ..] => "jpg",
        [0x89, (byte)'P', (byte)'N', (byte)'G', ..] => "png",
        [(byte)'R', (byte)'I', (byte)'F', (byte)'F', _, _, _, _, (byte)'W', (byte)'E', (byte)'B', (byte)'P', ..] =>
            "webp",
        _ => null,
    };

    /// <returns>The body; empty when it's over <see cref="MaxBytes"/> (a length the server didn't announce).</returns>
    private static async Task<byte[]> ReadAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        Stream body = await response.Content
            .ReadAsStreamAsync(cancellationToken)
            .ConfigureAwait(false);
        await using (body.ConfigureAwait(false))
        {
            using MemoryStream copy = new();
            byte[] buffer = new byte[81920];
            int read;
            while ((read = await body.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) > 0)
            {
                if (copy.Length + read > MaxBytes)
                {
                    return [];
                }

                copy.Write(buffer, 0, read);
            }

            return copy.ToArray();
        }
    }

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "A picture outside YouTube's picture hosts isn't downloaded: {Url}")]
    private partial void LogRefused(string url);

    [LoggerMessage(Level = LogLevel.Warning, Message = "The picture {Url} can't be downloaded: {Reason}")]
    private partial void LogFailed(Uri url, string reason);
}
