using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TonTuyau.Infrastructure.Accounts.Migrations
{
    /// <inheritdoc />
    public partial class YouTubeCommentsChoice : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // readComments: true becomes the top 500 (what it read), false off.
            migrationBuilder.AddColumn<string>(
                name: "comments",
                table: "youtube_settings",
                type: "text",
                nullable: false,
                defaultValue: "Off");

            migrationBuilder.AddColumn<int>(
                name: "comments_max",
                table: "youtube_settings",
                type: "integer",
                nullable: false,
                defaultValue: 500);

            migrationBuilder.Sql("UPDATE youtube_settings SET comments = 'Top' WHERE read_comments;");

            migrationBuilder.DropColumn(
                name: "read_comments",
                table: "youtube_settings");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "comments",
                table: "youtube_settings");

            migrationBuilder.DropColumn(
                name: "comments_max",
                table: "youtube_settings");

            migrationBuilder.AddColumn<bool>(
                name: "read_comments",
                table: "youtube_settings",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }
    }
}
