using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TonTuyau.Infrastructure.ApiKeys;
using TonTuyau.Infrastructure.Library;
using TonTuyau.Infrastructure.RunLogs;
using TonTuyau.Infrastructure.YouTube;

namespace TonTuyau.Infrastructure.Accounts;

/// <summary>
/// TonTuyau's PostgreSQL: the accounts, the keys that sign the login cookies (so a restart logs nobody out), the
/// runs' logs (1.4), the API keys, the YouTube cookie, the yt-dlp chosen, TonTuyau's own facts about videos and
/// channels (1.5: their YouTube state, refreshed metadata, chapters and comments), and the library itself (2.0: its
/// videos and channels, the source of truth Elasticsearch indexes). Table and column names are snake_case, as
/// PostgreSQL likes them.
/// </summary>
public sealed class TonTuyauDbContext(DbContextOptions<TonTuyauDbContext> options)
    : DbContext(options), IDataProtectionKeyContext
{
    public DbSet<AccountRow> Accounts => Set<AccountRow>();

    public DbSet<DataProtectionKey> DataProtectionKeys => Set<DataProtectionKey>();

    public DbSet<RunLogRow> RunLogLines => Set<RunLogRow>();

    public DbSet<ApiKeyRow> ApiKeys => Set<ApiKeyRow>();

    public DbSet<YouTubeCookieRow> YouTubeCookie => Set<YouTubeCookieRow>();

    public DbSet<YtdlpSettingsRow> YtdlpSettings => Set<YtdlpSettingsRow>();

    public DbSet<YouTubeVideoRow> YouTubeVideos => Set<YouTubeVideoRow>();

    public DbSet<YouTubeChannelRow> YouTubeChannels => Set<YouTubeChannelRow>();

    public DbSet<VideoCommentRow> VideoComments => Set<VideoCommentRow>();

    public DbSet<YouTubeSettingsRow> YouTubeSettings => Set<YouTubeSettingsRow>();

    public DbSet<VideoRow> Videos => Set<VideoRow>();

    public DbSet<VideoStateRow> VideoStates => Set<VideoStateRow>();

    public DbSet<CategoryRow> Categories => Set<CategoryRow>();

    public DbSet<ChannelRow> Channels => Set<ChannelRow>();

    public DbSet<ChannelStateRow> ChannelStates => Set<ChannelStateRow>();

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
        modelBuilder.Entity<YtdlpSettingsRow>(settings =>
        {
            settings.ToTable("ytdlp_settings");
            settings
                .Property(s => s.Id)
                .HasColumnName("id")
                .ValueGeneratedNever();
            settings
                .Property(s => s.Kind)
                .HasColumnName("kind")
                .HasConversion<string>();
            settings.Property(s => s.Version).HasColumnName("version");
            settings.Property(s => s.ChosenBy).HasColumnName("chosen_by");
            settings.Property(s => s.ChosenAt).HasColumnName("chosen_at");
            settings.Property(s => s.CheckedAt).HasColumnName("checked_at");
            settings.Property(s => s.Error).HasColumnName("error");
        });
        modelBuilder.Entity<YouTubeVideoRow>(video =>
        {
            video.ToTable("youtube_videos");
            video.HasKey(v => v.VideoId);
            video.Property(v => v.VideoId).HasColumnName("video_id");
            video.Property(v => v.ChannelId).HasColumnName("channel_id");
            video
                .HasOne<YouTubeChannelRow>()
                .WithMany()
                .HasForeignKey(v => v.ChannelId)
                .OnDelete(DeleteBehavior.Cascade);
            video
                .Property(v => v.State)
                .HasColumnName("state")
                .HasConversion<string>();
            video.Property(v => v.CheckedAt).HasColumnName("checked_at");
            video.Property(v => v.Reason).HasColumnName("reason");
            video.Property(v => v.RefreshedAt).HasColumnName("refreshed_at");
            video.Property(v => v.Title).HasColumnName("title");
            video.Property(v => v.ChannelName).HasColumnName("channel_name");
            video.Property(v => v.Description).HasColumnName("description");
            video.Property(v => v.Tags).HasColumnName("tags");
            video.Property(v => v.Categories).HasColumnName("categories");
            video.Property(v => v.Views).HasColumnName("views");
            video.Property(v => v.Likes).HasColumnName("likes");
            video.Property(v => v.CommentCount).HasColumnName("comment_count");
            video.Property(v => v.CommentsReadAt).HasColumnName("comments_read_at");
            video.OwnsMany(
                v => v.Chapters,
                chapter =>
                {
                    chapter.ToJson("chapters");
                    chapter.Property(c => c.Start).HasJsonPropertyName("start");
                    chapter.Property(c => c.End).HasJsonPropertyName("end");
                    chapter.Property(c => c.Title).HasJsonPropertyName("title");
                });
            video.Property(v => v.ThumbnailPath).HasColumnName("thumbnail_path");
        });
        modelBuilder.Entity<YouTubeChannelRow>(channel =>
        {
            channel.ToTable("youtube_channels");
            channel.HasKey(c => c.ChannelId);
            channel.Property(c => c.ChannelId).HasColumnName("channel_id");
            channel
                .Property(c => c.State)
                .HasColumnName("state")
                .HasConversion<string>();
            channel.Property(c => c.CheckedAt).HasColumnName("checked_at");
            channel.Property(c => c.Reason).HasColumnName("reason");
            channel.Property(c => c.RefreshedAt).HasColumnName("refreshed_at");
            channel.Property(c => c.Name).HasColumnName("name");
            channel.Property(c => c.Description).HasColumnName("description");
            channel.Property(c => c.Subscribers).HasColumnName("subscribers");
            channel.Property(c => c.Tags).HasColumnName("tags");
            channel.Property(c => c.ThumbPath).HasColumnName("thumb_path");
            channel.Property(c => c.BannerPath).HasColumnName("banner_path");
        });
        modelBuilder.Entity<YouTubeSettingsRow>(settings =>
        {
            settings.ToTable("youtube_settings");
            settings
                .Property(s => s.Id)
                .HasColumnName("id")
                .ValueGeneratedNever();
            settings
                .Property(s => s.Comments)
                .HasColumnName("comments")
                .HasConversion<string>();
            settings.Property(s => s.CommentsMax).HasColumnName("comments_max");
            settings.Property(s => s.PauseSeconds).HasColumnName("pause_seconds");
            settings.Property(s => s.HoldMinutes).HasColumnName("hold_minutes");
            settings.Property(s => s.ChangedBy).HasColumnName("changed_by");
            settings.Property(s => s.ChangedAt).HasColumnName("changed_at");
        });
        modelBuilder.Entity<VideoCommentRow>(comment =>
        {
            comment.ToTable("video_comments");
            comment.HasKey(c => new { c.VideoId, c.CommentId });
            comment.Property(c => c.VideoId).HasColumnName("video_id");
            comment.Property(c => c.CommentId).HasColumnName("comment_id");
            comment.Property(c => c.Position).HasColumnName("position");
            comment.Property(c => c.ParentId).HasColumnName("parent_id");
            comment.Property(c => c.Text).HasColumnName("text");
            comment.Property(c => c.Author).HasColumnName("author");
            comment.Property(c => c.AuthorId).HasColumnName("author_id");
            comment.Property(c => c.AuthorThumbnail).HasColumnName("author_thumbnail");
            comment.Property(c => c.Published).HasColumnName("published");
            comment.Property(c => c.Likes).HasColumnName("likes");
            comment.Property(c => c.Pinned).HasColumnName("pinned");
            comment.Property(c => c.ByUploader).HasColumnName("by_uploader");
            comment.Property(c => c.Hearted).HasColumnName("hearted");
            comment.HasIndex(c => new { c.VideoId, c.Position });
            comment
                .HasOne<YouTubeVideoRow>()
                .WithMany()
                .HasForeignKey(c => c.VideoId)
                .OnDelete(DeleteBehavior.Cascade);
        });
        modelBuilder.Entity<VideoRow>(MapVideos);
        modelBuilder.Entity<VideoStateRow>(MapVideoStates);
        modelBuilder.Entity<CategoryRow>(MapCategories);
        modelBuilder.Entity<ChannelRow>(MapChannels);
        modelBuilder.Entity<ChannelStateRow>(MapChannelStates);
    }

    private static void MapVideoStates(EntityTypeBuilder<VideoStateRow> state)
    {
        state.ToTable("video_states");
        state.HasKey(s => s.VideoId);
        state.Property(s => s.VideoId).HasColumnName("video_id");
        state
            .Property(s => s.State)
            .HasColumnName("state")
            .HasConversion<string>();
        state.Property(s => s.CheckedAt).HasColumnName("checked_at");
        state.Property(s => s.Reason).HasColumnName("reason");
        state.Property(s => s.RefreshedAt).HasColumnName("refreshed_at");
    }

    private static void MapCategories(EntityTypeBuilder<CategoryRow> category)
    {
        category.ToTable("categories");
        category.Property(c => c.Id).HasColumnName("id");
        category.Property(c => c.Name).HasColumnName("name");
        category
            .HasIndex(c => c.Name)
            .IsUnique();
    }

    private static void MapChannelStates(EntityTypeBuilder<ChannelStateRow> state)
    {
        state.ToTable("channel_states");
        state.HasKey(s => s.ChannelId);
        state.Property(s => s.ChannelId).HasColumnName("channel_id");
        state
            .Property(s => s.State)
            .HasColumnName("state")
            .HasConversion<string>();
        state.Property(s => s.CheckedAt).HasColumnName("checked_at");
        state.Property(s => s.Reason).HasColumnName("reason");
        state.Property(s => s.RefreshedAt).HasColumnName("refreshed_at");
    }

    private static void MapVideos(EntityTypeBuilder<VideoRow> video)
    {
        video.ToTable("videos");
        video.Property(v => v.Id).HasColumnName("id");
        video
            .Property(v => v.Origin)
            .HasColumnName("origin")
            .HasConversion<string>();
        video.Property(v => v.ChannelId).HasColumnName("channel_id");
        video.Property(v => v.ChannelName).HasColumnName("channel_name");
        video.Property(v => v.Title).HasColumnName("title");
        video.Property(v => v.Description).HasColumnName("description");
        video
            .Property(v => v.Type)
            .HasColumnName("type")
            .HasConversion<string>();
        video.Property(v => v.Published).HasColumnName("published");
        video.Property(v => v.Downloaded).HasColumnName("downloaded");
        video.Property(v => v.Duration).HasColumnName("duration");
        video.Property(v => v.MediaSize).HasColumnName("media_size");
        video.Property(v => v.MediaPath).HasColumnName("media_path");
        video.Property(v => v.MediaPathCorrected).HasColumnName("media_path_corrected");
        video.Property(v => v.ThumbnailPath).HasColumnName("thumbnail_path");
        video.Property(v => v.Tags).HasColumnName("tags");
        video.Property(v => v.CategoryId).HasColumnName("category_id");
        // A category stays while a video is in it.
        video
            .HasOne(v => v.Category)
            .WithMany()
            .HasForeignKey(v => v.CategoryId)
            .OnDelete(DeleteBehavior.Restrict);
        video
            .Navigation(v => v.Category)
            .AutoInclude();
        video.Property(v => v.Views).HasColumnName("views");
        video.Property(v => v.Likes).HasColumnName("likes");
        video.Property(v => v.Dislikes).HasColumnName("dislikes");
        video.Property(v => v.CommentCount).HasColumnName("comment_count");
        video.OwnsMany(
            v => v.Tracks,
            track =>
            {
                track.ToJson("tracks");
                track
                    .Property(t => t.Kind)
                    .HasJsonPropertyName("kind")
                    .HasConversion<string>();
                track.Property(t => t.Codec).HasJsonPropertyName("codec");
                track.Property(t => t.Bitrate).HasJsonPropertyName("bitrate");
                track.Property(t => t.Width).HasJsonPropertyName("width");
                track.Property(t => t.Height).HasJsonPropertyName("height");
            });
        video.Property(v => v.MediaMissing).HasColumnName("media_missing");
        video.OwnsOne(v => v.ThumbnailArt, MapArt<VideoRow>("thumbnail_art"));
        // Its state on YouTube: its row of video_states, written and read with it, deleted with it.
        video
            .HasOne(v => v.YouTube)
            .WithOne()
            .HasForeignKey<VideoStateRow>(s => s.VideoId)
            .OnDelete(DeleteBehavior.Cascade);
        video
            .Navigation(v => v.YouTube)
            .AutoInclude();
        video.OwnsMany(
            v => v.Chapters,
            chapter =>
            {
                chapter.ToJson("chapters");
                chapter.Property(c => c.Start).HasJsonPropertyName("start");
                chapter.Property(c => c.End).HasJsonPropertyName("end");
                chapter.Property(c => c.Title).HasJsonPropertyName("title");
            });
        video.Property(v => v.CommentsReadAt).HasColumnName("comments_read_at");
        // No foreign key to channels: TubeArchivist may list a video whose channel it can't give (a broken document);
        // the video is kept all the same, as the index always did.
        video.HasIndex(v => v.ChannelId);
        // The flagged files, few among many.
        video
            .HasIndex(v => v.MediaMissing)
            .HasFilter("media_missing");
    }

    private static void MapChannels(EntityTypeBuilder<ChannelRow> channel)
    {
        channel.ToTable("channels");
        channel.Property(c => c.Id).HasColumnName("id");
        channel
            .Property(c => c.Origin)
            .HasColumnName("origin")
            .HasConversion<string>();
        channel.Property(c => c.Name).HasColumnName("name");
        channel.Property(c => c.Description).HasColumnName("description");
        channel.Property(c => c.Subscribers).HasColumnName("subscribers");
        channel.Property(c => c.Tags).HasColumnName("tags");
        channel.Property(c => c.ThumbPath).HasColumnName("thumb_path");
        channel.Property(c => c.BannerPath).HasColumnName("banner_path");
        channel.Property(c => c.TvArtPath).HasColumnName("tv_art_path");
        channel.OwnsOne(c => c.ThumbArt, MapArt<ChannelRow>("thumb_art"));
        channel.OwnsOne(c => c.BannerArt, MapArt<ChannelRow>("banner_art"));
        // Its state on YouTube: its row of channel_states, written and read with it, deleted with it.
        channel
            .HasOne(c => c.YouTube)
            .WithOne()
            .HasForeignKey<ChannelStateRow>(s => s.ChannelId)
            .OnDelete(DeleteBehavior.Cascade);
        channel
            .Navigation(c => c.YouTube)
            .AutoInclude();
    }

    /// <summary>A picture's recorded versions, as a JSON column.</summary>
    private static Action<OwnedNavigationBuilder<TOwner, ArtVersionsRow>> MapArt<TOwner>(string column)
        where TOwner : class =>
        art =>
        {
            art.ToJson(column);
            art.Property(a => a.OriginalWidth).HasJsonPropertyName("original_width");
            art.Property(a => a.Widths).HasJsonPropertyName("widths");
        };
}
