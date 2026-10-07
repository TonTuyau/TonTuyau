namespace TonTuyau.TubeArchivist.Http;

/// <summary>Something tried to send a request that could modify TubeArchivist. It never left the process.</summary>
public sealed class ReadOnlyViolationException(HttpMethod method, Uri? uri)
    : InvalidOperationException(
        $"TonTuyau never modifies TubeArchivist: {method} {uri?.AbsolutePath} was blocked before sending.")
{
    public HttpMethod Method { get; } = method;
}
