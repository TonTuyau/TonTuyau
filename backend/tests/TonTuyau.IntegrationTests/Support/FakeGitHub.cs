using System.Collections.Concurrent;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace TonTuyau.IntegrationTests.Support;

/// <summary>
/// GitHub, for yt-dlp's versions (1.5): the release lists of yt-dlp/yt-dlp and yt-dlp/yt-dlp-nightly-builds (API), and
/// each release's files (downloads): a "yt-dlp_linux" that's a script printing its version, and the SHA2-256SUMS of it.
/// </summary>
public sealed class FakeGitHub : IAsyncDisposable
{
    private static readonly string[] WithProgram = ["yt-dlp_linux", "SHA2-256SUMS", "yt-dlp"];
    private static readonly string[] WithoutProgram = ["yt-dlp"];

    private readonly WebApplication _app;

    private FakeGitHub(WebApplication app) => _app = app;

    public string Url { get; private set; } = "";

    public List<FakeRelease> Official { get; } = [];

    public List<FakeRelease> Nightly { get; } = [];

    /// <summary>Releases whose published checksum doesn't match their program.</summary>
    public HashSet<string> WrongChecksum { get; } = new(StringComparer.Ordinal);

    public ConcurrentQueue<string> Requests { get; } = new();

    public static async Task<FakeGitHub> StartAsync()
    {
        WebApplicationBuilder builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Logging.ClearProviders();
        WebApplication app = builder.Build();
        FakeGitHub fake = new(app);
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

    /// <summary>The program of a version: what it prints for --version.</summary>
    public static string Program(string version) => $"#!/usr/bin/env bash\necho {version}\n";

    public int Downloads(string version) =>
        Requests.Count(r => r.EndsWith($"/{version}/yt-dlp_linux", StringComparison.Ordinal));

    public ValueTask DisposeAsync() => _app.DisposeAsync();

    /// <summary>A release as GitHub's API lists it (the fields TonTuyau reads).</summary>
    private static object ReleaseJson(FakeRelease release) => new
    {
        tag_name = release.Version,
        published_at = release.PublishedAt,
        draft = false,
        assets = (release.HasLinuxProgram ? WithProgram : WithoutProgram).Select(name => new { name }),
    };

    private async Task HandleAsync(HttpContext http)
    {
        string path = http.Request.Path.Value ?? "/";
        Requests.Enqueue(path);
        string[] parts = path.Trim('/').Split('/');
        // /repos/yt-dlp/<repo>/releases?per_page=100&page=N
        if (parts is ["repos", "yt-dlp", string repo, "releases"])
        {
            int page = int.Parse(http.Request.Query["page"].FirstOrDefault() ?? "1", CultureInfo.InvariantCulture);
            int size = int.Parse(http.Request.Query["per_page"].FirstOrDefault() ?? "30", CultureInfo.InvariantCulture);
            List<FakeRelease> list = repo == "yt-dlp-nightly-builds" ? Nightly : Official;
            object[] releases = [.. list
                .OrderByDescending(r => r.PublishedAt)
                .Skip((page - 1) * size)
                .Take(size)
                .Select(ReleaseJson)];
            http.Response.ContentType = "application/json";
            await http.Response.WriteAsync(JsonSerializer.Serialize(releases));
            return;
        }

        // /yt-dlp/<repo>/releases/download/<tag>/<file>
        if (parts is ["yt-dlp", _, "releases", "download", string tag, string file])
        {
            string program = Program(tag);
            if (file == "yt-dlp_linux")
            {
                await http.Response.WriteAsync(program);
                return;
            }

            if (file == "SHA2-256SUMS")
            {
                string published = WrongChecksum.Contains(tag) ? program + "tampered" : program;
                string hash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(published)));
                await http.Response.WriteAsync($"{new string('0', 64)}  yt-dlp\n{hash}  yt-dlp_linux\n");
                return;
            }
        }

        http.Response.StatusCode = StatusCodes.Status404NotFound;
    }
}
