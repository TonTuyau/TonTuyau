using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TonTuyau.Infrastructure.Accounts.Migrations
{
    /// <inheritdoc />
    public partial class DefaultTabs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // The settings row there before: the three tabs, as before B6 (the model has no default, see the context).
            migrationBuilder.AddColumn<bool>(
                name: "default_shorts",
                table: "youtube_settings",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "default_streams",
                table: "youtube_settings",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "default_videos",
                table: "youtube_settings",
                type: "boolean",
                nullable: false,
                defaultValue: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "default_shorts",
                table: "youtube_settings");

            migrationBuilder.DropColumn(
                name: "default_streams",
                table: "youtube_settings");

            migrationBuilder.DropColumn(
                name: "default_videos",
                table: "youtube_settings");
        }
    }
}
