namespace TonTuyau.Application.YouTube;

/// <summary>Which comments a refresh reads with each video (1.5), the YouTube page's setting.</summary>
public enum YouTubeCommentsChoice
{
    /// <summary>None: refreshes stay quick (the default).</summary>
    Off,

    /// <summary>YouTube's top ones, answers included, up to <see cref="YouTubeSettings.CommentsMax"/>.</summary>
    Top,

    /// <summary>Every comment and answer: slow on a video with many.</summary>
    All,
}
