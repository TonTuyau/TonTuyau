using TonTuyau.Application.YouTube;

namespace TonTuyau.YouTube.UnitTests.Support;

/// <summary>The cookie a test gives the client: always the same, or none.</summary>
internal sealed class FixedCookieSource(string? content) : IYouTubeCookieSource
{
    public Task<string?> GetAsync(CancellationToken cancellationToken) => Task.FromResult(content);
}
