using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using TonTuyau.Infrastructure.ApiKeys;
using TonTuyau.Infrastructure.RunLogs;
using TonTuyau.Infrastructure.YouTube;

namespace TonTuyau.Infrastructure.Accounts;

/// <summary>
/// TonTuyau's PostgreSQL: the accounts, the keys that sign the login cookies (so a restart logs nobody out), the
/// runs' logs (1.4), the API keys and the YouTube cookie (1.5).
/// Table and column names are snake_case, as PostgreSQL likes them.
/// </summary>
public sealed class TonTuyauDbContext(DbContextOptions<TonTuyauDbContext> options)
    : DbContext(options), IDataProtectionKeyContext
{
    public DbSet<AccountRow> Accounts => Set<AccountRow>();

    public DbSet<DataProtectionKey> DataProtectionKeys => Set<DataProtectionKey>();

    public DbSet<RunLogRow> RunLogLines => Set<RunLogRow>();

    public DbSet<ApiKeyRow> ApiKeys => Set<ApiKeyRow>();

    public DbSet<YouTubeCookieRow> YouTubeCookie => Set<YouTubeCookieRow>();

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
        modelBuilder.Entity<ApiKeyRow>(key =>
        {
            key.ToTable("api_keys");
            key.Property(k => k.Id).HasColumnName("id");
            key.Property(k => k.Name).HasColumnName("name");
            key.Property(k => k.NameKey).HasColumnName("name_key");
            key.Property(k => k.Prefix).HasColumnName("prefix");
            key.Property(k => k.Hash).HasColumnName("hash");
            key
                .Property(k => k.Kind)
                .HasColumnName("kind")
                .HasConversion<string>();
            key.Property(k => k.Enabled).HasColumnName("enabled");
            key.Property(k => k.CreatedAt).HasColumnName("created_at");
            key.Property(k => k.CreatedBy).HasColumnName("created_by");
            key.Property(k => k.LastUsedAt).HasColumnName("last_used_at");
            // A request's key is found by its hash.
            key
                .HasIndex(k => k.Hash)
                .IsUnique();
            key
                .HasIndex(k => k.NameKey)
                .IsUnique();
        });
        modelBuilder.Entity<YouTubeCookieRow>(cookie =>
        {
            cookie.ToTable("youtube_cookie");
            cookie
                .Property(c => c.Id)
                .HasColumnName("id")
                .ValueGeneratedNever();
            cookie.Property(c => c.Content).HasColumnName("content");
            cookie.Property(c => c.ReceivedAt).HasColumnName("received_at");
            cookie.Property(c => c.ReceivedBy).HasColumnName("received_by");
            cookie
                .Property(c => c.State)
                .HasColumnName("state")
                .HasConversion<string>();
            cookie.Property(c => c.CheckedAt).HasColumnName("checked_at");
            cookie.Property(c => c.Reason).HasColumnName("reason");
        });
    }
}
