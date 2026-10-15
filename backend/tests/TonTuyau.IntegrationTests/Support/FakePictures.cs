using System.Collections.Concurrent;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SkiaSharp;

namespace TonTuyau.IntegrationTests.Support;

/// <summary>
/// YouTube's picture hosts for the tests (1.5): every address answers a 1280×720 JPEG, those whose path ends
/// <c>.webp</c> a WebP, those with <c>missing</c> in them a 404.
/// </summary>
public sealed class FakePictures : IAsyncDisposable
{
    public static readonly byte[] Jpeg = Encode(SKEncodedImageFormat.Jpeg);
    public static readonly byte[] Webp = Encode(SKEncodedImageFormat.Webp);

    private readonly WebApplication _app;

    private FakePictures(WebApplication app) => _app = app;

    public string Url { get; private set; } = "";

    public ConcurrentQueue<string> Requests { get; } = new();

    public static async Task<FakePictures> StartAsync()
    {
        WebApplicationBuilder builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Logging.ClearProviders();
        WebApplication app = builder.Build();
        FakePictures fake = new(app);
        app.Run(fake.HandleAsync);
        await app.StartAsync();
        fake.Url = app.Services
            .GetRequiredService<IServer>()
            .Features
            .Get<IServerAddressesFeature>()!
            .Addresses
            .Single();
        return fake;
    }

    public ValueTask DisposeAsync() => _app.DisposeAsync();

    private static byte[] Encode(SKEncodedImageFormat format)
    {
        using SKBitmap bitmap = new(1280, 720);
        using SKCanvas canvas = new(bitmap);
        canvas.Clear(SKColors.OrangeRed);
        using SKData data = bitmap.Encode(format, 80);
        return data.ToArray();
    }

    private async Task HandleAsync(HttpContext context)
    {
        string path = context.Request.Path.Value ?? "";
        Requests.Enqueue(path);
        if (path.Contains("missing", StringComparison.Ordinal))
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }

        bool webp = path.EndsWith(".webp", StringComparison.Ordinal);
        context.Response.ContentType = webp ? "image/webp" : "image/jpeg";
        await context.Response.Body.WriteAsync(webp ? Webp : Jpeg);
    }
}
