using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TonTuyau.Infrastructure.Accounts.Migrations
{
    /// <inheritdoc />
    public partial class CommentsChaptersYouTubeSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "chapters",
                table: "youtube_videos",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "comments_read_at",
                table: "youtube_videos",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "video_comments",
                columns: table => new
                {
                    video_id = table.Column<string>(type: "text", nullable: false),
                    comment_id = table.Column<string>(type: "text", nullable: false),
                    position = table.Column<int>(type: "integer", nullable: false),
                    parent_id = table.Column<string>(type: "text", nullable: true),
                    text = table.Column<string>(type: "text", nullable: false),
                    author = table.Column<string>(type: "text", nullable: false),
                    author_id = table.Column<string>(type: "text", nullable: true),
                    author_thumbnail = table.Column<string>(type: "text", nullable: true),
                    published = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    likes = table.Column<long>(type: "bigint", nullable: true),
                    pinned = table.Column<bool>(type: "boolean", nullable: false),
                    by_uploader = table.Column<bool>(type: "boolean", nullable: false),
                    hearted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_video_comments", x => new { x.video_id, x.comment_id });
                    table.ForeignKey(
                        name: "FK_video_comments_youtube_videos_video_id",
                        column: x => x.video_id,
                        principalTable: "youtube_videos",
                        principalColumn: "video_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "youtube_settings",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false),
                    read_comments = table.Column<bool>(type: "boolean", nullable: false),
                    changed_by = table.Column<string>(type: "text", nullable: true),
                    changed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_youtube_settings", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_video_comments_video_id_position",
                table: "video_comments",
                columns: new[] { "video_id", "position" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "video_comments");

            migrationBuilder.DropTable(
                name: "youtube_settings");

            migrationBuilder.DropColumn(
                name: "chapters",
                table: "youtube_videos");

            migrationBuilder.DropColumn(
                name: "comments_read_at",
                table: "youtube_videos");
        }
    }
}
