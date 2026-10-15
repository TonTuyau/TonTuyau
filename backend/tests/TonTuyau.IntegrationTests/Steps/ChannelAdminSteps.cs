using TonTuyau.IntegrationTests.Support;

namespace TonTuyau.IntegrationTests.Steps;

/// <summary>A channel's admin page (1.5): its pages of videos, asked with the admin's session.</summary>
[Binding]
public sealed class ChannelAdminSteps(ApiSteps api, AuthSteps auth)
{
    [When("I request the next page of {string} with my session")]
    public Task NextPage(string path)
    {
        string cursor = ApiSteps.Scalar(api.At("nextCursor"))
            ?? throw new InvalidOperationException("There is no next page.");
        string separator = path.Contains('?', StringComparison.Ordinal) ? "&" : "?";
        return auth.RequestWithSession($"{path}{separator}cursor={Uri.EscapeDataString(cursor)}");
    }
}
