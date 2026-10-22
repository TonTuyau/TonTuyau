using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TonTuyau.Infrastructure.Accounts.Migrations
{
    /// <inheritdoc />
    public partial class YouTubeSyncs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "sync_every_hours",
                table: "youtube_settings",
                type: "integer",
                nullable: false,
                defaultValue: 6);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "synced_at",
                table: "channel_subscriptions",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "sync_every_hours",
                table: "youtube_settings");

            migrationBuilder.DropColumn(
                name: "synced_at",
                table: "channel_subscriptions");
        }
    }
}
