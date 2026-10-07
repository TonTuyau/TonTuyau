using System.Net.Http.Headers;

namespace TonTuyau.TubeArchivist.Http;

/// <summary>Adds TubeArchivist's <c>Authorization: Token …</c> header.</summary>
public sealed class TokenAuthHandler(string token) : DelegatingHandler
{
    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        request.Headers.Authorization = new AuthenticationHeaderValue("Token", token);
        return base.SendAsync(request, cancellationToken);
    }
}
