using TonTuyau.Application.Validation;
using TonTuyau.Application.YouTube;

namespace TonTuyau.Application.UnitTests.YouTube;

public sealed class NetscapeCookiesTests
{
    private const string Session = ".youtube.com\tTRUE\t/\tTRUE\t1820000000\tLOGIN_INFO\tAFmmF2sw";
    private const string HttpOnly = "#HttpOnly_.youtube.com\tTRUE\t/\tTRUE\t1820000000\t__Secure-3PSID\tg.a000";
    private const string Www = "www.youtube.com\tFALSE\t/\tFALSE\t0\tPREF\tf6=40000000";

    [Fact]
    public void Only_the_youtube_com_cookies_are_kept_under_the_header()
    {
        string file = string.Join(
            "\r\n",
            "# Netscape HTTP Cookie File",
            "# a comment",
            "",
            Session,
            ".google.com\tTRUE\t/\tTRUE\t1820000000\tSID\tsecret-of-google",
            HttpOnly,
            ".notyoutube.com\tTRUE\t/\tTRUE\t0\tX\tY",
            Www,
            "youtube.com broken line without tabs");

        Assert.Equal(
            $"# Netscape HTTP Cookie File\n{Session}\n{HttpOnly}\n{Www}\n",
            NetscapeCookies.KeepYouTube(file));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("   ")]
    [InlineData("# Netscape HTTP Cookie File\n.google.com\tTRUE\t/\tTRUE\t0\tSID\tx\n")]
    [InlineData("LOGIN_INFO=AFmmF2sw; PREF=f6=40000000")]
    public void A_file_without_a_youtube_com_cookie_is_refused(string? text)
    {
        ValidationException ex = Assert.Throws<ValidationException>(() => NetscapeCookies.KeepYouTube(text));

        Assert.Equal("cookies", Assert.Single(ex.Errors).Field);
    }

    [Fact]
    public void A_file_too_long_is_refused()
    {
        string text = Session + "\n" + new string('#', NetscapeCookies.MaxLength);

        Assert.Throws<ValidationException>(() => NetscapeCookies.KeepYouTube(text));
    }
}
