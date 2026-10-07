using System.Net;
using TonTuyau.Api.Hosting;
using TonTuyau.IntegrationTests.Support;

namespace TonTuyau.IntegrationTests.Steps;

[Binding]
public sealed class HealthSteps(ScenarioState state)
{
    [Given("the backend is configured with valid settings")]
    public static void GivenValidSettings()
    {
        // TestSettings starts out valid.
    }

    [Given("the setting {string} is empty")]
    public void GivenSettingIsEmpty(string name) => state.Settings.Set(name, "");

    [Given("the setting {string} is {string}")]
    public void GivenSettingIs(string name, string value) => state.Settings.Set(name, value);

    [When("the backend starts")]
    public void WhenTheBackendStarts()
    {
        try
        {
            _ = state.Backend.Services;
        }
#pragma warning disable CA1031 // The scenario asserts on whatever startup throws.
        catch (Exception ex)
#pragma warning restore CA1031
        {
            state.Error = ex;
        }
    }

    /// <summary><c>{video:key}</c> and <c>{channel:key}</c> in the path stand for the ids of seeded items.</summary>
    [When("I request {string}")]
    public async Task WhenIRequest(string path)
    {
        Uri uri = new(TestIds.Expand(path), UriKind.Relative);
        using HttpRequestMessage request = new(HttpMethod.Get, uri);
        await state.SendAsync(request);
    }

    [Then("the response status is {int}")]
    public void ThenTheResponseStatusIs(int status)
    {
        Assert.NotNull(state.Response);
        Assert.Equal((HttpStatusCode)status, state.Response.StatusCode);
    }

    [Then("the response body is {string}")]
    public void ThenTheResponseBodyIs(string body)
    {
        Assert.NotNull(state.Response);
        Assert.Equal(body, System.Text.Encoding.UTF8.GetString(state.ResponseBody));
    }

    [Then("the response carries TonTuyau's version in {string}")]
    public void ThenTheResponseCarriesTheVersion(string header)
    {
        Assert.NotNull(state.Response);
        Assert.True(state.Response.Headers.TryGetValues(header, out IEnumerable<string>? values));
        Assert.Equal([AppVersion.Value], values);
    }

    [Then("it fails to start with an error about {string}")]
    public void ThenItFailsToStartWithAnErrorAbout(string setting)
    {
        Assert.NotNull(state.Error);
        Assert.Contains(setting, state.Error.ToString(), StringComparison.Ordinal);
    }

    [Then("the error does not reveal the TubeArchivist token")]
    public void ThenTheErrorDoesNotRevealTheToken()
    {
        Assert.NotNull(state.Error);
        Assert.DoesNotContain(TestSettings.Token, state.Error.ToString(), StringComparison.Ordinal);
    }
}
