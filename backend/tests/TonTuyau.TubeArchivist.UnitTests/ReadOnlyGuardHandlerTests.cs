using TonTuyau.TubeArchivist.Http;
using TonTuyau.TubeArchivist.UnitTests.Support;

namespace TonTuyau.TubeArchivist.UnitTests;

public sealed class ReadOnlyGuardHandlerTests
{
    public static readonly string[] WriteMethods
        = ["POST", "PUT", "PATCH", "DELETE", "OPTIONS", "TRACE", "CONNECT", "PROPFIND", "MKCOL", "purge"];

    [Theory, CombinatorialData]
    public async Task Write_methods_never_reach_TubeArchivist(
        [CombinatorialMemberData(nameof(WriteMethods))] string method,
        bool async)
    {
        FixtureHandler fake = new(_ => FixtureHandler.Respond(System.Net.HttpStatusCode.OK, "{}"));
        using HttpClient client = new(new ReadOnlyGuardHandler(fake))
        {
            BaseAddress = new Uri("http://tubearchivist.test/")
        };
        using HttpRequestMessage request = new(new HttpMethod(method), "api/video/abc/");

        ReadOnlyViolationException ex = async
            ? await Assert.ThrowsAsync<ReadOnlyViolationException>(
                () => client.SendAsync(request, TestContext.Current.CancellationToken))
            : Assert.Throws<ReadOnlyViolationException>(
                () => client.Send(request, TestContext.Current.CancellationToken));

        Assert.Equal(method, ex.Method.Method);
        Assert.Empty(fake.Requests);
    }

    [Theory, CombinatorialData]
    public async Task Get_and_head_pass_through([CombinatorialValues("GET", "HEAD")] string method)
    {
        FixtureHandler fake = new(_ => FixtureHandler.Respond(System.Net.HttpStatusCode.OK, "{}"));
        using HttpClient client = new(new ReadOnlyGuardHandler(fake))
        {
            BaseAddress = new Uri("http://tubearchivist.test/")
        };
        using HttpRequestMessage request = new(new HttpMethod(method), "api/ping/");

        using HttpResponseMessage response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Single(fake.Requests);
    }

    [Fact]
    public void Only_get_and_head_are_allowed()
    {
        Assert.True(ReadOnlyGuardHandler.IsAllowed(HttpMethod.Get));
        Assert.True(ReadOnlyGuardHandler.IsAllowed(HttpMethod.Head));
        Assert.False(ReadOnlyGuardHandler.IsAllowed(HttpMethod.Post));
        Assert.False(ReadOnlyGuardHandler.IsAllowed(HttpMethod.Query));
    }
}
