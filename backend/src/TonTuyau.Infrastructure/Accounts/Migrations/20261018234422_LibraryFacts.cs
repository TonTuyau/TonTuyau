using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TonTuyau.Infrastructure.Accounts.Migrations
{
    /// <inheritdoc />
    public partial class LibraryFacts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_video_comments_youtube_videos_video_id",
                table: "video_comments");

            migrationBuilder.DropTable(
                name: "youtube_videos");

            migrationBuilder.DropTable(
                name: "youtube_channels");

            // 2.0 starts from an empty library (no upgrade path): the comments read in 1.5 belonged to the facts just
            // dropped, and would point to videos the new table doesn't have yet.
            migrationBuilder.Sql("DELETE FROM video_comments;");

            migrationBuilder.AddForeignKey(
                name: "FK_video_comments_videos_video_id",
                table: "video_comments",
                column: "video_id",
                principalTable: "videos",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_video_comments_videos_video_id",
                table: "video_comments");

            migrationBuilder.CreateTable(
                name: "youtube_channels",
                columns: table => new
                {
                    channel_id = table.Column<string>(type: "text", nullable: false),
                    banner_path = table.Column<string>(type: "text", nullable: true),
                    checked_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    description = table.Column<string>(type: "text", nullable: true),
                    name = table.Column<string>(type: "text", nullable: true),
                    reason = table.Column<string>(type: "text", nullable: true),
                    refreshed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    state = table.Column<string>(type: "text", nullable: false),
                    subscribers = table.Column<long>(type: "bigint", nullable: true),
                    tags = table.Column<List<string>>(type: "text[]", nullable: false),
                    thumb_path = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_youtube_channels", x => x.channel_id);
                });

            migrationBuilder.CreateTable(
                name: "youtube_videos",
                columns: table => new
                {
                    video_id = table.Column<string>(type: "text", nullable: false),
                    categories = table.Column<List<string>>(type: "text[]", nullable: false),
                    channel_id = table.Column<string>(type: "text", nullable: false),
                    channel_name = table.Column<string>(type: "text", nullable: true),
                    checked_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    comment_count = table.Column<long>(type: "bigint", nullable: true),
                    comments_read_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    description = table.Column<string>(type: "text", nullable: true),
                    likes = table.Column<long>(type: "bigint", nullable: true),
                    reason = table.Column<string>(type: "text", nullable: true),
                    refreshed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    state = table.Column<string>(type: "text", nullable: false),
                    tags = table.Column<List<string>>(type: "text[]", nullable: false),
                    thumbnail_path = table.Column<string>(type: "text", nullable: true),
                    title = table.Column<string>(type: "text", nullable: true),
                    views = table.Column<long>(type: "bigint", nullable: true),
                    chapters = table.Column<string>(type: "jsonb", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_youtube_videos", x => x.video_id);
                    table.ForeignKey(
                        name: "FK_youtube_videos_youtube_channels_channel_id",
                        column: x => x.channel_id,
                        principalTable: "youtube_channels",
                        principalColumn: "channel_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_youtube_videos_channel_id",
                table: "youtube_videos",
                column: "channel_id");

            migrationBuilder.AddForeignKey(
                name: "FK_video_comments_youtube_videos_video_id",
                table: "video_comments",
                column: "video_id",
                principalTable: "youtube_videos",
                principalColumn: "video_id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
