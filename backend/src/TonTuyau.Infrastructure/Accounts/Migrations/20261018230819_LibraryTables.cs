using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

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
                name: "categories",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    name = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_categories", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "channels",
                columns: table => new
                {
                    id = table.Column<string>(type: "text", nullable: false),
                    origin = table.Column<string>(type: "text", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    description = table.Column<string>(type: "text", nullable: false),
                    subscribers = table.Column<long>(type: "bigint", nullable: false),
                    tags = table.Column<List<string>>(type: "text[]", nullable: false),
                    thumb_path = table.Column<string>(type: "text", nullable: true),
                    banner_path = table.Column<string>(type: "text", nullable: true),
                    tv_art_path = table.Column<string>(type: "text", nullable: true),
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
                    published = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    downloaded = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    duration = table.Column<TimeSpan>(type: "interval", nullable: false),
                    media_size = table.Column<long>(type: "bigint", nullable: false),
                    media_path = table.Column<string>(type: "text", nullable: false),
                    media_path_corrected = table.Column<bool>(type: "boolean", nullable: false),
                    thumbnail_path = table.Column<string>(type: "text", nullable: false),
                    tags = table.Column<List<string>>(type: "text[]", nullable: false),
                    category_id = table.Column<int>(type: "integer", nullable: true),
                    views = table.Column<long>(type: "bigint", nullable: false),
                    likes = table.Column<long>(type: "bigint", nullable: false),
                    dislikes = table.Column<long>(type: "bigint", nullable: false),
                    comment_count = table.Column<long>(type: "bigint", nullable: true),
                    media_missing = table.Column<bool>(type: "boolean", nullable: false),
                    comments_read_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    chapters = table.Column<string>(type: "jsonb", nullable: true),
                    thumbnail_art = table.Column<string>(type: "jsonb", nullable: true),
                    tracks = table.Column<string>(type: "jsonb", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_videos", x => x.id);
                    table.ForeignKey(
                        name: "FK_videos_categories_category_id",
                        column: x => x.category_id,
                        principalTable: "categories",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "channel_states",
                columns: table => new
                {
                    channel_id = table.Column<string>(type: "text", nullable: false),
                    state = table.Column<string>(type: "text", nullable: false),
                    checked_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    reason = table.Column<string>(type: "text", nullable: true),
                    refreshed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_channel_states", x => x.channel_id);
                    table.ForeignKey(
                        name: "FK_channel_states_channels_channel_id",
                        column: x => x.channel_id,
                        principalTable: "channels",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "video_states",
                columns: table => new
                {
                    video_id = table.Column<string>(type: "text", nullable: false),
                    state = table.Column<string>(type: "text", nullable: false),
                    checked_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    reason = table.Column<string>(type: "text", nullable: true),
                    refreshed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_video_states", x => x.video_id);
                    table.ForeignKey(
                        name: "FK_video_states_videos_video_id",
                        column: x => x.video_id,
                        principalTable: "videos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_categories_name",
                table: "categories",
                column: "name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_videos_category_id",
                table: "videos",
                column: "category_id");

            migrationBuilder.CreateIndex(
                name: "IX_videos_channel_id",
                table: "videos",
                column: "channel_id");

            migrationBuilder.CreateIndex(
                name: "IX_videos_media_missing",
                table: "videos",
                column: "media_missing",
                filter: "media_missing");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "channel_states");

            migrationBuilder.DropTable(
                name: "video_states");

            migrationBuilder.DropTable(
                name: "channels");

            migrationBuilder.DropTable(
                name: "videos");

            migrationBuilder.DropTable(
                name: "categories");
        }
    }
}
