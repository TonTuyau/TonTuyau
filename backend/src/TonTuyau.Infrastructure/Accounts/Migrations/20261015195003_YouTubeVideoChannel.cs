using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TonTuyau.Infrastructure.Accounts.Migrations
{
    /// <inheritdoc />
    public partial class YouTubeVideoChannel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "channel_id",
                table: "youtube_videos",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "channel_name",
                table: "youtube_videos",
                type: "text",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_youtube_videos_channel_id",
                table: "youtube_videos",
                column: "channel_id");

            migrationBuilder.AddForeignKey(
                name: "FK_youtube_videos_youtube_channels_channel_id",
                table: "youtube_videos",
                column: "channel_id",
                principalTable: "youtube_channels",
                principalColumn: "channel_id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_youtube_videos_youtube_channels_channel_id",
                table: "youtube_videos");

            migrationBuilder.DropIndex(
                name: "IX_youtube_videos_channel_id",
                table: "youtube_videos");

            migrationBuilder.DropColumn(
                name: "channel_id",
                table: "youtube_videos");

            migrationBuilder.DropColumn(
                name: "channel_name",
                table: "youtube_videos");
        }
    }
}
