using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TonTuyau.Infrastructure.Accounts.Migrations
{
    /// <inheritdoc />
    public partial class SyncEveryMinutes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 2.1.0: the YouTube sync's interval in minutes; each one's choice kept (6 hours: 360 minutes).
            migrationBuilder.AddColumn<int>(
                name: "sync_every_minutes",
                table: "youtube_settings",
                type: "integer",
                nullable: false,
                defaultValue: 360);

            migrationBuilder.Sql("UPDATE youtube_settings SET sync_every_minutes = sync_every_hours * 60;");

            migrationBuilder.DropColumn(
                name: "sync_every_hours",
                table: "youtube_settings");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "sync_every_hours",
                table: "youtube_settings",
                type: "integer",
                nullable: false,
                defaultValue: 6);

            // Back to whole hours: rounded up, so an eager interval stays as eager as hours allow (0 stays by hand).
            migrationBuilder.Sql(
                "UPDATE youtube_settings SET sync_every_hours = CEIL(sync_every_minutes / 60.0)::integer;");

            migrationBuilder.DropColumn(
                name: "sync_every_minutes",
                table: "youtube_settings");
        }
    }
}
