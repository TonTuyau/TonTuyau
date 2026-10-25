using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TonTuyau.Infrastructure.Accounts.Migrations
{
    /// <inheritdoc />
    public partial class RetryWithoutCookie : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 2.2.1: the row there gets the default: a download's format matching none with the cookie, tried without.
            migrationBuilder.AddColumn<bool>(
                name: "retry_without_cookie",
                table: "youtube_settings",
                type: "boolean",
                nullable: false,
                defaultValue: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "retry_without_cookie",
                table: "youtube_settings");
        }
    }
}
