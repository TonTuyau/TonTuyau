namespace TonTuyau.Application.YouTube;

/// <summary>A picture downloaded from YouTube (1.5).</summary>
/// <param name="Extension">Its format, read from its content: <c>jpg</c>, <c>png</c> or <c>webp</c>.</param>
public sealed record YouTubeImage(byte[] Content, string Extension);
