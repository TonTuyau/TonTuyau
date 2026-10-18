using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TonTuyau.Infrastructure.Accounts.Migrations
{
    /// <inheritdoc />
    public partial class LibraryTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "channels",
                columns: table => new
                {
                    id = table.Column<string>(type: "text", nullable: false),
                    origin = table.Column<string>(type: "text", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    description = table.Column<string>(type: "text", nullable: false),
                    subscribers = table.Column<long>(type: "bigint", nullable: false),
                    active = table.Column<bool>(type: "boolean", nullable: false),
                    last_refreshed = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    tags = table.Column<List<string>>(type: "text[]", nullable: false),
                    tabs = table.Column<List<string>>(type: "text[]", nullable: false),
                    thumb_path = table.Column<string>(type: "text", nullable: true),
                    banner_path = table.Column<string>(type: "text", nullable: true),
                    tv_art_path = table.Column<string>(type: "text", nullable: true),
                    content_hash = table.Column<string>(type: "text", nullable: false),
                    seen_in_run = table.Column<string>(type: "text", nullable: true),
                    youtube_state = table.Column<string>(type: "text", nullable: true),
                    youtube_checked_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    youtube_reason = table.Column<string>(type: "text", nullable: true),
                    youtube_refreshed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    youtube_name = table.Column<string>(type: "text", nullable: true),
                    youtube_description = table.Column<string>(type: "text", nullable: true),
                    youtube_subscribers = table.Column<long>(type: "bigint", nullable: true),
                    youtube_tags = table.Column<List<string>>(type: "text[]", nullable: false),
                    youtube_thumb_path = table.Column<string>(type: "text", nullable: true),
                    youtube_banner_path = table.Column<string>(type: "text", nullable: true),
                    banner_art = table.Column<string>(type: "jsonb", nullable: true),
                    thumb_art = table.Column<string>(type: "jsonb", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_channels", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "videos",
                columns: table => new
                {
                    id = table.Column<string>(type: "text", nullable: false),
                    origin = table.Column<string>(type: "text", nullable: false),
                    channel_id = table.Column<string>(type: "text", nullable: false),
                    channel_name = table.Column<string>(type: "text", nullable: false),
                    title = table.Column<string>(type: "text", nullable: false),
                    description = table.Column<string>(type: "text", nullable: false),
                    type = table.Column<string>(type: "text", nullable: false),
                    active = table.Column<bool>(type: "boolean", nullable: false),
                    published = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    downloaded = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    last_refreshed = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    duration = table.Column<TimeSpan>(type: "interval", nullable: false),
                    media_size = table.Column<long>(type: "bigint", nullable: false),
                    media_path = table.Column<string>(type: "text", nullable: false),
                    media_path_corrected = table.Column<bool>(type: "boolean", nullable: false),
                    thumbnail_path = table.Column<string>(type: "text", nullable: false),
                    tags = table.Column<List<string>>(type: "text[]", nullable: false),
                    categories = table.Column<List<string>>(type: "text[]", nullable: false),
                    playlist_ids = table.Column<List<string>>(type: "text[]", nullable: false),
                    views = table.Column<long>(type: "bigint", nullable: false),
                    likes = table.Column<long>(type: "bigint", nullable: false),
                    dislikes = table.Column<long>(type: "bigint", nullable: false),
                    content_hash = table.Column<string>(type: "text", nullable: false),
                    seen_in_run = table.Column<string>(type: "text", nullable: true),
                    media_missing = table.Column<bool>(type: "boolean", nullable: false),
                    youtube_state = table.Column<string>(type: "text", nullable: true),
                    youtube_checked_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    youtube_reason = table.Column<string>(type: "text", nullable: true),
                    youtube_refreshed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    youtube_title = table.Column<string>(type: "text", nullable: true),
                    youtube_channel_name = table.Column<string>(type: "text", nullable: true),
                    youtube_description = table.Column<string>(type: "text", nullable: true),
                    youtube_tags = table.Column<List<string>>(type: "text[]", nullable: false),
                    youtube_categories = table.Column<List<string>>(type: "text[]", nullable: false),
                    youtube_views = table.Column<long>(type: "bigint", nullable: true),
                    youtube_likes = table.Column<long>(type: "bigint", nullable: true),
                    youtube_comment_count = table.Column<long>(type: "bigint", nullable: true),
                    youtube_thumbnail_path = table.Column<string>(type: "text", nullable: true),
                    comments_read_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    chapters = table.Column<string>(type: "jsonb", nullable: true),
                    thumbnail_art = table.Column<string>(type: "jsonb", nullable: true),
                    tracks = table.Column<string>(type: "jsonb", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_videos", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_channels_seen_in_run",
                table: "channels",
                column: "seen_in_run");

            migrationBuilder.CreateIndex(
                name: "IX_videos_channel_id",
                table: "videos",
                column: "channel_id");

            migrationBuilder.CreateIndex(
                name: "IX_videos_media_missing",
                table: "videos",
                column: "media_missing",
                filter: "media_missing");

            migrationBuilder.CreateIndex(
                name: "IX_videos_seen_in_run",
                table: "videos",
                column: "seen_in_run");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "channels");

            migrationBuilder.DropTable(
                name: "videos");
        }
    }
}
