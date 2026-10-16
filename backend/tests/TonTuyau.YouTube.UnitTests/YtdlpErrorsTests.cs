using TonTuyau.Application.YouTube;

namespace TonTuyau.YouTube.UnitTests;

public sealed class YtdlpErrorsTests
{
    [Theory]
    [InlineData("ERROR: [youtube] abcdefghijk: Video unavailable. This video has been removed by the uploader")]
    [InlineData("ERROR: [youtube] abcdefghijk: Private video. Sign in if you've been granted access to this video")]
    [InlineData("ERROR: [youtube] abcdefghijk: This video is private")]
    [InlineData(
        "ERROR: [youtube] abcdefghijk: Video unavailable. This video is no longer available because the YouTube "
        + "account associated with this video has been terminated.")]
    [InlineData(
        "ERROR: [youtube] abcdefghijk: Video unavailable. This video has been removed for violating YouTube's Terms "
        + "of Service")]
    [InlineData("ERROR: [youtube:tab] UCxxxxxxxxxxxxxxxxxxxxxx: This channel does not exist.")]
    public void YouTube_own_words_for_gone_are_gone(string line)
    {
        Assert.Equal(YouTubeAnswer.Gone, YtdlpErrors.Classify([line], 1).Answer);
    }

    [Theory]
    [InlineData("ERROR: [youtube] abcdefghijk: Sign in to confirm you’re not a bot.")]
    [InlineData("ERROR: [youtube] abcdefghijk: Private video. Sign in to confirm you’re not a bot.")]
    [InlineData("ERROR: [youtube] abcdefghijk: Unable to download API page: HTTP Error 429: Too Many Requests")]
    public void A_bot_check_or_too_many_requests_is_YouTube_refusing_never_gone(string line)
    {
        Assert.Equal(YouTubeAnswer.Refused, YtdlpErrors.Classify([line], 1).Answer);
    }

    [Theory]
    [InlineData(
        "ERROR: [youtube] abcdefghijk: Video unavailable. The uploader has not made this video available in your "
        + "country")]
    [InlineData("ERROR: [youtube] abcdefghijk: Sign in to confirm your age.")]
    [InlineData("ERROR: [youtube] abcdefghijk: Join this channel to get access to members-only content.")]
    [InlineData("ERROR: [youtube] abcdefghijk: Video unavailable")]
    [InlineData("ERROR: Unable to download webpage: <urlopen error [Errno -3] Temporary failure in name resolution>")]
    public void Anything_else_is_a_failed_check(string line)
    {
        Assert.Equal(YouTubeAnswer.CheckFailed, YtdlpErrors.Classify([line], 1).Answer);
    }

    [Fact]
    public void The_reason_is_yt_dlp_s_message_without_its_prefix_and_warnings_are_left_out()
    {
        (YouTubeAnswer _, string reason) = YtdlpErrors.Classify(
            [
                "WARNING: [youtube] abcdefghijk: a warning",
                "ERROR: [youtube] abcdefghijk: Video unavailable. This video has been removed by the uploader",
            ],
            1);

        Assert.Equal("Video unavailable. This video has been removed by the uploader", reason);
    }

    [Fact]
    public void Without_an_error_line_the_check_failed_with_the_exit_code()
    {
        Assert.Equal(
            (YouTubeAnswer.CheckFailed, "yt-dlp failed without saying why (exit code 2)."),
            YtdlpErrors.Classify(["WARNING: something"], 2));
    }
}
