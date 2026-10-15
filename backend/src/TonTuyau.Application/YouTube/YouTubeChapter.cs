namespace TonTuyau.Application.YouTube;

/// <summary>A chapter of a video (1.5), as YouTube marks it: from where to where, in seconds, and its title.</summary>
public sealed record YouTubeChapter(double StartSeconds, double EndSeconds, string Title);
