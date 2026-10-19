using Elastic.Clients.Elasticsearch;
using Elastic.Transport;
using Microsoft.Extensions.DependencyInjection;
using TonTuyau.Application.Comments;
using TonTuyau.Application.Configuration;
using TonTuyau.Application.Dashboard;
using TonTuyau.Application.Store;
using TonTuyau.Application.Sync;
using TonTuyau.Application.Tasks;
using TonTuyau.Application.YouTube;
using TonTuyau.Infrastructure.Elasticsearch;
using TonTuyau.Infrastructure.Library;
using TonTuyau.Infrastructure.Storage;
using TonTuyau.Infrastructure.YouTube;

namespace TonTuyau.Infrastructure;

public static class SearchStoreServiceCollectionExtensions
{
    public const string ReadyTag = "ready";

    /// <summary>TonTuyau's Elasticsearch: client, indices, repositories, readiness check, index creation at startup.</summary>
    public static IServiceCollection AddSearchStore(this IServiceCollection services)
    {
        services.AddSingleton(sp => EsClient.Create(sp.GetRequiredService<TonTuyauOptions>().Elasticsearch));
        services.AddSingleton(sp => new IndexNames(sp.GetRequiredService<TonTuyauOptions>().Elasticsearch.IndexPrefix));
        services.AddSingleton<IIndexManager, IndexManager>();
        services.AddSingleton<VideoIndex>();
        services.AddSingleton<ChannelIndex>();
        services.AddSingleton<IDashboardRepository, DashboardRepository>();
        services.AddSingleton<ITaskRepository, TaskRepository>();
        services.AddSingleton<SearchStoreReadiness>();
        services.AddSingleton<IndexRebuildState>();
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton(SyncLockSettings.Default);
        services.AddSingleton<ISyncStateStore, SyncStateStore>();
        services.AddSingleton(sp => new FileArtStore(sp.GetRequiredService<TonTuyauOptions>().Storage.ArtRoot));
        services.AddSingleton<IArtStore>(sp => sp.GetRequiredService<FileArtStore>());
        services.AddSingleton<IArtResizer>(sp => new SkiaArtResizer(sp.GetRequiredService<FileArtStore>()));
        services.AddSingleton<IMediaLibrary>(
            sp => new MediaLibrary(sp.GetRequiredService<TonTuyauOptions>().Storage.MediaRoot));
        services.AddHostedService<IndexInitializer>();
        // What a refresh learned (1.5), in the library's rows in PostgreSQL (AddAccounts).
        services.AddSingleton<IYouTubeFactsSource, YouTubeFactsSource>();
        services.AddSingleton<IYouTubeFactsStore, YouTubeFactsStore>();
        services.AddSingleton<ICommentRepository, CommentRepository>();
        // The library itself (2.0), in PostgreSQL: a database context per call, like the facts, so the command line
        // (no PostgreSQL) can still build what it doesn't use. Writes go there first, then to the index.
        services.AddSingleton<VideoStore>();
        services.AddSingleton<IVideoStore>(sp => sp.GetRequiredService<VideoStore>());
        services.AddSingleton<ChannelStore>();
        services.AddSingleton<IChannelStore>(sp => sp.GetRequiredService<ChannelStore>());
        services.AddSingleton<IVideoRepository, LibraryVideoRepository>();
        services.AddSingleton<IChannelRepository, LibraryChannelRepository>();
        services.AddHealthChecks().AddCheck<SearchStoreHealthCheck>("elasticsearch", tags: [ReadyTag]);
        return services;
    }
}
