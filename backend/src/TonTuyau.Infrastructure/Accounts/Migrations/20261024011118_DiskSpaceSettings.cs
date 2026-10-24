using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TonTuyau.Infrastructure.Accounts.Migrations
{
    /// <inheritdoc />
    public partial class DiskSpaceSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 2.2.0: the disk space. The row there gets the defaults: a warning under 50 GB, the downloads held then.
            migrationBuilder.AddColumn<bool>(
                name: "hold_below_min_free",
                table: "youtube_settings",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<int>(
                name: "min_free_gb",
                table: "youtube_settings",
                type: "integer",
                nullable: false,
                defaultValue: 50);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "hold_below_min_free",
                table: "youtube_settings");

            migrationBuilder.DropColumn(
                name: "min_free_gb",
                table: "youtube_settings");
        }
    }
}
