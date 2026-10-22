using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TonTuyau.Infrastructure.Accounts.Migrations
{
    /// <inheritdoc />
    public partial class ChannelSubscriptions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "channel_subscriptions",
                columns: table => new
                {
                    channel_id = table.Column<string>(type: "text", nullable: false),
                    videos = table.Column<bool>(type: "boolean", nullable: false),
                    shorts = table.Column<bool>(type: "boolean", nullable: false),
                    streams = table.Column<bool>(type: "boolean", nullable: false),
                    subscribed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    subscribed_by = table.Column<string>(type: "text", nullable: true),
                    origin = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_channel_subscriptions", x => x.channel_id);
                    table.ForeignKey(
                        name: "FK_channel_subscriptions_channels_channel_id",
                        column: x => x.channel_id,
                        principalTable: "channels",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "channel_subscriptions");
        }
    }
}
