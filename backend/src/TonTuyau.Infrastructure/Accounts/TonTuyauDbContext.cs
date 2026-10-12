using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using TonTuyau.Infrastructure.RunLogs;

namespace TonTuyau.Infrastructure.Accounts;

/// <summary>
/// TonTuyau's PostgreSQL: the accounts, the keys that sign the login cookies (so a restart logs nobody out), and the
/// runs' logs (1.4).
/// Table and column names are snake_case, as PostgreSQL likes them.
/// </summary>
public sealed class TonTuyauDbContext(DbContextOptions<TonTuyauDbContext> options)
    : DbContext(options), IDataProtectionKeyContext
{
    public DbSet<AccountRow> Accounts => Set<AccountRow>();

    public DbSet<DataProtectionKey> DataProtectionKeys => Set<DataProtectionKey>();

    public DbSet<RunLogRow> RunLogLines => Set<RunLogRow>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);
        modelBuilder.Entity<AccountRow>(account =>
        {
            account.ToTable("accounts");
            account.Property(a => a.Id).HasColumnName("id");
            account.Property(a => a.Username).HasColumnName("username");
            account.Property(a => a.PasswordHash).HasColumnName("password_hash");
            account.Property(a => a.CreatedAt).HasColumnName("created_at");
            account.Property(a => a.LastLoginAt).HasColumnName("last_login_at");
            account
                .HasIndex(a => a.Username)
                .IsUnique();
        });
        modelBuilder.Entity<DataProtectionKey>(key =>
        {
            key.ToTable("data_protection_keys");
            key.Property(k => k.Id).HasColumnName("id");
            key.Property(k => k.FriendlyName).HasColumnName("friendly_name");
            key.Property(k => k.Xml).HasColumnName("xml");
        });
        modelBuilder.Entity<RunLogRow>(line =>
        {
            line.ToTable("run_log_lines");
            line.Property(l => l.Id).HasColumnName("id");
            line.Property(l => l.RunId).HasColumnName("run_id");
            line.Property(l => l.At).HasColumnName("at");
            line.Property(l => l.Level).HasColumnName("level");
            line.Property(l => l.Message).HasColumnName("message");
            // A run's lines, in order: the only way they're read.
            line.HasIndex(l => new { l.RunId, l.Id });
        });
    }
}
