namespace TonTuyau.TubeArchivist.Http;

/// <summary>
/// Outermost handler of the TubeArchivist HttpClient: lets only GET and HEAD through, so no code path,
/// retry policy or bug can send a request that modifies TubeArchivist.
/// </summary>
public sealed class ReadOnlyGuardHandler : DelegatingHandler
{
    public ReadOnlyGuardHandler()
    {
    }

    public ReadOnlyGuardHandler(HttpMessageHandler innerHandler)
        : base(innerHandler)
    {
    }

    public static bool IsAllowed(HttpMethod method) => method == HttpMethod.Get || method == HttpMethod.Head;

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        EnsureAllowed(request);
        return base.SendAsync(request, cancellationToken);
    }

    protected override HttpResponseMessage Send(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        EnsureAllowed(request);
        return base.Send(request, cancellationToken);
    }

    private static void EnsureAllowed(HttpRequestMessage request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!IsAllowed(request.Method))
        {
            throw new ReadOnlyViolationException(request.Method, request.RequestUri);
        }
    }
}
