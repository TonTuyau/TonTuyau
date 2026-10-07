using TonTuyau.TubeArchivist.Http;
using TonTuyau.TubeArchivist.Validation;

namespace TonTuyau.TubeArchivist.UnitTests.Support;

internal static class GatewayFactory
{
    public const string Token = "unit-test-token-7c1e";

    /// <summary>Same handler order as the app (guard → token → TA), minus resilience.</summary>
    public static TubeArchivistGateway Create(FixtureHandler handler, bool withToken = true)
    {
        HttpMessageHandler inner = handler;
        if (withToken)
        {
            inner = new TokenAuthHandler(Token) { InnerHandler = inner };
        }

#pragma warning disable CA2000 // The HttpClient owns and disposes the handler chain.
        HttpClient http = new(new ReadOnlyGuardHandler(inner))
        {
            BaseAddress = new Uri("http://tubearchivist.test/")
        };
#pragma warning restore CA2000
        return new TubeArchivistGateway(http, new VideoDtoValidator(), new ChannelDtoValidator());
    }
}
