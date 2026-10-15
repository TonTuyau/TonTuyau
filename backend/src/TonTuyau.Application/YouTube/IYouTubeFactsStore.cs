namespace TonTuyau.Application.YouTube;

/// <summary>Records what a refresh learned (1.5): TonTuyau's facts in PostgreSQL, each replaced as a whole.</summary>
public interface IYouTubeFactsStore
{
    Task SaveVideoAsync(VideoFacts facts, CancellationToken cancellationToken);

    Task SaveChannelAsync(ChannelFacts facts, CancellationToken cancellationToken);
}
