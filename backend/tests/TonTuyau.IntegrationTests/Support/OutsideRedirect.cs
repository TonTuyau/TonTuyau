using System.Net.Http;

namespace TonTuyau.IntegrationTests.Support;

/// <summary>
/// What the backend's clients for outside services reach in the tests (GitHub for yt-dlp's releases, YouTube's picture
/// hosts, 1.5): never the real ones. Without a fake, every request fails as when the service can't be reached; with
/// one, it goes there, path and query unchanged (one fake serves every host of its service).
/// </summary>
public sealed class OutsideRedirect(string service, Uri? fake) : DelegatingHandler(new SocketsHttpHandler())
{
    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (fake is null)
        {
            throw new HttpRequestException($"The tests never reach {service} ({request.RequestUri}).");
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
