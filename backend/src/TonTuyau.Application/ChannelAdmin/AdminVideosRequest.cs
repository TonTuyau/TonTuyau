namespace TonTuyau.Application.ChannelAdmin;

/// <param name="State">Only the videos in this state on YouTube (<c>notChecked</c>, <c>onYouTube</c>, …); null: all.</param>
/// <param name="Q">Words of the title (or tags, description); null or blank: every video.</param>
public sealed record AdminVideosRequest(string ChannelId, string? State, string? Q, string? Cursor, int? Size);
