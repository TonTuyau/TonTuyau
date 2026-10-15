using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using TonTuyau.Application.Accounts;
using TonTuyau.Application.ApiKeys;
using TonTuyau.Application.Configuration;
using TonTuyau.Application.YouTube;
using TonTuyau.Infrastructure.Accounts;
using TonTuyau.Infrastructure.ApiKeys;
using TonTuyau.Infrastructure.YouTube;

namespace TonTuyau.Infrastructure;

public static class AccountsServiceCollectionExtensions
{
    /// <summary>
    /// TonTuyau's PostgreSQL (EF Core), the accounts, the API keys, the YouTube cookie and the yt-dlp chosen in it.
    /// </summary>
    public static IServiceCollection AddAccounts(this IServiceCollection services)
    {
        services.AddDbContext<TonTuyauDbContext>((sp, o) =>
            o.UseNpgsql(ConnectionString(sp.GetRequiredService<TonTuyauOptions>().Database)));
        services.AddScoped<IAccountRepository, AccountRepository>();
        services.AddSingleton<IPasswordHasher, Argon2idPasswordHasher>();
        services.AddScoped<AccountService>();
        services.AddScoped<IApiKeyRepository, ApiKeyRepository>();
        services.AddScoped<ApiKeyService>();
        services.AddScoped<IYouTubeCookieRepository, YouTubeCookieRepository>();
        services.AddSingleton<IYouTubeCookieSource, YouTubeCookieSource>();
        services.AddScoped<YouTubeCookieService>();
        services.AddScoped<IYtdlpSettingsRepository, YtdlpSettingsRepository>();
        services.AddScoped<YtdlpService>();
        return services;
    }

    /// <summary>
    /// The schema up to date (the migrations: a database matter, here) and the first account there (a rule, the
    /// service's), before the backend answers anything.
    /// </summary>
    public static async Task InitializeAccountsAsync(this IServiceProvider services)
    {
        AsyncServiceScope scope = services.CreateAsyncScope();
        await using (scope.ConfigureAwait(false))
        {
            await scope.ServiceProvider
                .GetRequiredService<TonTuyauDbContext>()
                .Database
                .MigrateAsync(CancellationToken.None)
                .ConfigureAwait(false);
            await scope.ServiceProvider
                .GetRequiredService<AccountService>()
                .EnsureAdminAsync(CancellationToken.None)
                .ConfigureAwait(false);
        }
    }

    private static string ConnectionString(DatabaseOptions database) =>
        new NpgsqlConnectionStringBuilder
        {
            Host = database.Host,
            Database = database.Name,
            Username = database.Username,
            Password = database.Password,
        }.ConnectionString;
}
