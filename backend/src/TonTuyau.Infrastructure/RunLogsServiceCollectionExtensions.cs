using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TonTuyau.Application.RunLogs;
using TonTuyau.Infrastructure.RunLogs;

namespace TonTuyau.Infrastructure;

public static class RunLogsServiceCollectionExtensions
{
    /// <summary>
    /// The runs' logs, in the PostgreSQL of <see cref="AccountsServiceCollectionExtensions.AddAccounts"/>: the web host
    /// only, where the runs run.
    /// </summary>
    public static IServiceCollection AddRunLogs(this IServiceCollection services)
    {
        services.AddScoped<IRunLogRepository, RunLogRepository>();
        services.AddSingleton<RunLogWriter>();
        services.AddHostedService(sp => sp.GetRequiredService<RunLogWriter>());
        services.AddSingleton<ILoggerProvider, RunLogLoggerProvider>();
        return services;
    }
}
