using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using TonTuyau.Application.Configuration;
using TonTuyau.Infrastructure.Accounts;

namespace TonTuyau.Infrastructure;

public static class AccountsServiceCollectionExtensions
{
    /// <summary>TonTuyau's PostgreSQL (EF Core) and the accounts in it.</summary>
    public static IServiceCollection AddAccounts(this IServiceCollection services)
    {
        services.AddDbContext<TonTuyauDbContext>((sp, o) =>
            o.UseNpgsql(ConnectionString(sp.GetRequiredService<TonTuyauOptions>().Database)));
        services.AddScoped<AccountService>();
        return services;
    }

    /// <summary>The schema up to date and the first account there, before the backend answers anything.</summary>
    public static async Task InitializeAccountsAsync(this IServiceProvider services)
    {
        AsyncServiceScope scope = services.CreateAsyncScope();
        await using (scope.ConfigureAwait(false))
        {
            await scope.ServiceProvider
                .GetRequiredService<AccountService>()
                .InitializeAsync(CancellationToken.None)
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
