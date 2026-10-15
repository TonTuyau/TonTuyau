using System.Net.Http;

namespace TonTuyau.IntegrationTests.Support;

/// <summary>
/// What the backend's GitHub client (yt-dlp's releases, 1.5) reaches in the tests: never the real GitHub. Without a fake
/// GitHub, every request fails as when GitHub can't be reached; with one, it goes there, path and query unchanged (the
/// API's paths and the downloads' don't overlap, so one fake serves both).
/// </summary>
public sealed class GitHubRedirect(Uri? fake) : DelegatingHandler(new SocketsHttpHandler())
{
    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (fake is null)
        {
            throw new HttpRequestException($"The tests never reach GitHub ({request.RequestUri}).");
        }

        UriBuilder target = new(request.RequestUri!)
        {
            Scheme = fake.Scheme,
            Host = fake.Host,
            Port = fake.Port,
        };
        request.RequestUri = target.Uri;
        return base.SendAsync(request, cancellationToken);
    }
}
