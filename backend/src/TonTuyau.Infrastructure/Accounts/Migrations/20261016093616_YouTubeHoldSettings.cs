using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TonTuyau.Infrastructure.Accounts.Migrations
{
    /// <inheritdoc />
    public partial class YouTubeHoldSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // The settings saved before keep their choice of comments, with the defaults of the new ones.
            migrationBuilder.AddColumn<int>(
                name: "hold_minutes",
                table: "youtube_settings",
                type: "integer",
                nullable: false,
                defaultValue: 60);

            migrationBuilder.AddColumn<int>(
                name: "pause_seconds",
                table: "youtube_settings",
                type: "integer",
                nullable: false,
                defaultValue: 10);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "hold_minutes",
                table: "youtube_settings");

            migrationBuilder.DropColumn(
                name: "pause_seconds",
                table: "youtube_settings");
        }
    }
}
