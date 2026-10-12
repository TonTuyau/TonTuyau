using System.Globalization;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using Npgsql;

namespace TonTuyau.IntegrationTests.Support;

/// <summary>
/// One real PostgreSQL for the whole test run (the compose file's image), started by the first scenario. Each
/// scenario has a database of its own, which the backend creates when it applies its migrations.
/// </summary>
public static class PostgresContainer
{
    public const string Image = "postgres:17";
    public const string Username = "postgres";
    public const string Password = "integration-db-password";

    private static readonly SemaphoreSlim Gate = new(1, 1);
    private static IContainer? _container;

    /// <summary><c>host:port</c>, as <c>TONTUYAU_DB_HOST</c> takes it.</summary>
    public static async Task<string> GetHostAsync()
    {
        await Gate.WaitAsync().ConfigureAwait(false);
        try
        {
            if (_container is null)
            {
                IContainer container = new ContainerBuilder(Image)
                    .WithEnvironment("POSTGRES_PASSWORD", Password)
                    // Throwaway data: no need to wait for the disk. Scenarios run in parallel, each with its pool.
                    .WithCommand("-c", "fsync=off", "-c", "synchronous_commit=off", "-c", "max_connections=300")
                    .WithPortBinding(5432, true)
                    // Over TCP: during its first-start setup the server only listens on its Unix socket.
                    .WithWaitStrategy(Wait.ForUnixContainer().UntilCommandIsCompleted(
                        "pg_isready",
                        "-h",
                        "127.0.0.1",
                        "-U",
                        Username))
                    .Build();
                await container.StartAsync().ConfigureAwait(false);
                _container = container;
            }

            string port = _container.GetMappedPublicPort(5432).ToString(CultureInfo.InvariantCulture);
            return $"{_container.Hostname}:{port}";
        }
        finally
        {
            Gate.Release();
        }
    }

    /// <summary>Drops one scenario's database (its backend stopped; <c>FORCE</c> closes what its pool left open).</summary>
    public static async Task DropDatabaseAsync(string name)
    {
        if (_container is null)
        {
            return;
        }

        NpgsqlConnectionStringBuilder builder = new()
        {
            Host = await GetHostAsync().ConfigureAwait(false),
            Database = "postgres",
            Username = Username,
            Password = Password,
            Pooling = false,
        };
        NpgsqlConnection connection = new(builder.ConnectionString);
        await using (connection.ConfigureAwait(false))
        {
            await connection.OpenAsync().ConfigureAwait(false);
            // The name is the scenario's own ("t" + hex digits), never input.
#pragma warning disable CA2100
            NpgsqlCommand drop = new($"DROP DATABASE IF EXISTS \"{name}\" WITH (FORCE)", connection);
#pragma warning restore CA2100
            await using (drop.ConfigureAwait(false))
            {
                await drop
                    .ExecuteNonQueryAsync()
                    .ConfigureAwait(false);
            }
        }
    }

    public static async Task StopAsync()
    {
        if (_container is not null)
        {
            await _container.DisposeAsync().ConfigureAwait(false);
            _container = null;
        }
    }
}
