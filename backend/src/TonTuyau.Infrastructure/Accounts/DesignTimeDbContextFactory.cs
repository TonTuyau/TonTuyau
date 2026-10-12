using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace TonTuyau.Infrastructure.Accounts;

/// <summary>
/// For <c>dotnet ef migrations add</c> only: it needs the model, never a connection, and starting the real backend
/// would need its whole configuration.
/// </summary>
public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<TonTuyauDbContext>
{
    public TonTuyauDbContext CreateDbContext(string[] args) =>
        new(new DbContextOptionsBuilder<TonTuyauDbContext>()
            .UseNpgsql("Host=localhost")
            .Options);
}
