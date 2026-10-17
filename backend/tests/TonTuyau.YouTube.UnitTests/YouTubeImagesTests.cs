using System.Net;
using Microsoft.Extensions.Logging.Abstractions;
using TonTuyau.Application.YouTube;

namespace TonTuyau.YouTube.UnitTests;

public sealed class YouTubeImagesTests
{
    private static readonly byte[] Jpeg = [0xFF, 0xD8, 0xFF, 0xE0, 0, 0x10];
    private static readonly byte[] Png = [0x89, (byte)'P', (byte)'N', (byte)'G', 0x0D, 0x0A];
    private static readonly byte[] Webp = "RIFF\0\0\0\0WEBPVP8 "u8.ToArray();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public void The_format_is_read_from_the_picture_itself()
    {
        Assert.Equal("jpg", YouTubeImages.FormatOf(Jpeg));
        Assert.Equal("png", YouTubeImages.FormatOf(Png));
        Assert.Equal("webp", YouTubeImages.FormatOf(Webp));
        Assert.Null(YouTubeImages.FormatOf("<html>"u8));
        Assert.Null(YouTubeImages.FormatOf([]));
    }

    [Theory]
    [InlineData("https://evil.example/vi/k3Fq8xZ0aQw/maxresdefault.jpg")]
    [InlineData("http://i.ytimg.com/vi/k3Fq8xZ0aQw/maxresdefault.jpg")]
    [InlineData("https://i.ytimg.com.evil.example/x.jpg")]
    [InlineData("not a url")]
    public async Task Only_YouTube_s_picture_hosts_over_HTTPS_are_asked(string url)
    {
        Answering handler = new(HttpStatusCode.OK, Jpeg);

        YouTubeImage? image = await Images(handler).GetAsync(url, Token);

        Assert.Null(image);
        Assert.Equal(0, handler.Calls);
    }

    [Theory]
    [InlineData("https://i.ytimg.com/vi/k3Fq8xZ0aQw/maxresdefault.jpg")]
    [InlineData("https://yt3.googleusercontent.com/ytc/avatar=s900")]
    [InlineData("https://yt3.ggpht.com/banner=w1060")]
    public async Task A_picture_from_YouTube_comes_with_its_format(string url)
    {
        YouTubeImage? image = await Images(new Answering(HttpStatusCode.OK, Webp)).GetAsync(url, Token);

        Assert.NotNull(image);
        Assert.Equal("webp", image.Extension);
        Assert.Equal(Webp, image.Content);
    }

    [Theory]
    [InlineData(HttpStatusCode.NotFound, true)]
    [InlineData(HttpStatusCode.OK, false)]
    public async Task An_error_or_something_else_than_a_picture_is_none(HttpStatusCode status, bool picture)
    {
        Answering handler = new(status, picture ? Jpeg : "<html>"u8.ToArray());

        YouTubeImage? image = await Images(handler).GetAsync("https://i.ytimg.com/vi/x/default.jpg", Token);

        Assert.Null(image);
    }

    [Fact]
    public async Task A_picture_over_10_MB_is_none()
    {
        byte[] huge = new byte[YouTubeImages.MaxBytes + 1];
        Jpeg.CopyTo(huge, 0);

        YouTubeImage? image = await Images(new Answering(HttpStatusCode.OK, huge, announceLength: false))
            .GetAsync("https://i.ytimg.com/vi/x/default.jpg", Token);

        Assert.Null(image);
    }

    private static YouTubeImages Images(HttpMessageHandler handler) =>
        new(new HttpClient(handler), NullLogger<YouTubeImages>.Instance);

    /// <summary>Answers every request the same; counts them.</summary>
    private sealed class Answering(HttpStatusCode status, byte[] body, bool announceLength = true) : HttpMessageHandler
    {
        public int Calls { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Calls++;
            HttpContent content = announceLength
                ? new ByteArrayContent(body)
                : new StreamContent(new MemoryStream(body));
            if (!announceLength)
            {
                content.Headers.ContentLength = null;
            }

            return Task.FromResult(new HttpResponseMessage(status) { Content = content });
        }
    }
}
