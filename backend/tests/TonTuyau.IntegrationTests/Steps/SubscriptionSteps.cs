using System.Text;
using Microsoft.Extensions.DependencyInjection;
using TonTuyau.Application.Library;
using TonTuyau.Application.Store;
using TonTuyau.IntegrationTests.Support;

namespace TonTuyau.IntegrationTests.Steps;

/// <summary>
/// The channels TonTuyau is subscribed to (2.0, B4): TubeArchivist's taken at the import, "Add a channel" (a task of
/// the Tasks page and the command line), and a channel's admin page. Channels are named as in the fake TubeArchivist
/// ("alpha"); a handle's answer is the channel's (<see cref="YouTubeSteps.Answers"/>).
/// </summary>
[Binding]
public sealed class SubscriptionSteps(ScenarioState state, SyncSteps sync, YouTubeSteps youTube, AuthSteps auth)
{
    private ISubscriptionStore Subscriptions => state.Backend.Services.GetRequiredService<ISubscriptionStore>();

    [Given("TubeArchivist is subscribed to the channel {string}")]
    public void SubscribedOnTubeArchivist(string name) => sync.Fake.Mutate(() =>
    {
        int index = sync.Fake.Channels.FindIndex(c => c.Name == name);
        sync.Fake.Channels[index] = sync.Fake.Channels[index] with { Subscribed = true };
    });

    /// <summary>After "YouTube has the channel": yt-dlp answers its handle's page with the same channel.</summary>
    [Given("YouTube has the channel {string} at the handle {string}")]
    public void AtHandle(string name, string handle) =>
        File.Copy(
            Path.Combine(youTube.Answers, FakeTubeArchivist.ChannelId(name) + ".json"),
            Path.Combine(youTube.Answers, handle + ".json"));

    [When("I add the channel {string} with my session")]
    public Task Add(string target) =>
        auth.PostWithSession($"/api/admin/tasks/addChannel/runs?target={Uri.EscapeDataString(Target(target))}");

    [When("I add the channel {string} with the tabs {string} with my session")]
    public Task AddWithTabs(string target, string tabs) =>
        auth.PostWithSession(
            $"/api/admin/tasks/addChannel/runs?target={Uri.EscapeDataString(Target(target))}&tabs={tabs}");

    [When("I request the admin page of the channel {string} with my session")]
    public Task AdminPage(string name) =>
        auth.RequestWithSession($"/api/admin/channels/{ChannelIdOf(name)}");

    [When("I subscribe to the channel {string} with {string} with my session")]
    public async Task Subscribe(string name, string body)
    {
        using HttpRequestMessage request = new(
            HttpMethod.Put,
            new Uri($"/api/admin/channels/{ChannelIdOf(name)}/subscription", UriKind.Relative))
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json"),
        };
        await auth.SendAsync(request);
    }

    [When("I unsubscribe from the channel {string} with my session")]
    public async Task Unsubscribe(string name)
    {
        using HttpRequestMessage request = new(
            HttpMethod.Delete,
            new Uri($"/api/admin/channels/{ChannelIdOf(name)}/subscription", UriKind.Relative));
        await auth.SendAsync(request);
    }

    /// <param name="tabs">As the API names them: <c>videos,shorts,streams</c> or some of them.</param>
    /// <param name="origin">tubeArchivist | tonTuyau.</param>
    [Then("TonTuyau is subscribed to the channel {string}: {string}, from {word}")]
    public async Task IsSubscribed(string name, string tabs, string origin)
    {
        ChannelSubscription? subscription = await Subscriptions.GetAsync(
            FakeTubeArchivist.ChannelId(name),
            CancellationToken.None);
        Assert.NotNull(subscription);
        Assert.Equal(ChannelTabs.Parse(tabs), subscription.Tabs);
        Assert.Equal(Enum.Parse<LibraryOrigin>(origin, ignoreCase: true), subscription.Origin);
    }

    [Then("TonTuyau isn't subscribed to the channel {string}")]
    public async Task IsNotSubscribed(string name) =>
        Assert.Null(await Subscriptions.GetAsync(FakeTubeArchivist.ChannelId(name), CancellationToken.None));

    /// <summary>A channel's name ("alpha"), or what an admin pastes as it is (an id, a handle, a link).</summary>
    private static string Target(string target) =>
        target.Contains('@', StringComparison.Ordinal) || target.Contains('/', StringComparison.Ordinal)
            ? target
            : FakeTubeArchivist.ChannelId(target);

    /// <summary>A channel the fake TubeArchivist names; another name is one the library doesn't have.</summary>
    private static string ChannelIdOf(string name) => FakeTubeArchivist.ChannelId(name);
}
