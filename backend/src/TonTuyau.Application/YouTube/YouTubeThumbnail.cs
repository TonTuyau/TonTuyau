namespace TonTuyau.Application.YouTube;

/// <summary>A picture YouTube offers (a thumbnail, a channel's avatar or banner); sizes when YouTube gives them.</summary>
public sealed record YouTubeThumbnail(string Url, int? Width, int? Height);
