using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TonTuyau.Infrastructure.Accounts.Migrations
{
    /// <inheritdoc />
    public partial class AccountRoles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "password_hash",
                table: "accounts",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AddColumn<bool>(
                name: "disabled",
                table: "accounts",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "display_name",
                table: "accounts",
                type: "text",
                nullable: true);

            // 2.3.0: the accounts made before are local admins, as they were.
            migrationBuilder.AddColumn<string>(
                name: "role",
                table: "accounts",
                type: "text",
                nullable: false,
                defaultValue: "Admin");

            migrationBuilder.AddColumn<string>(
                name: "source",
                table: "accounts",
                type: "text",
                nullable: false,
                defaultValue: "Local");

            migrationBuilder.AddColumn<string>(
                name: "stamp",
                table: "accounts",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "username_key",
                table: "accounts",
                type: "text",
                nullable: false,
                defaultValue: "");

            // Their name's key, and a stamp each: the sessions made before 2.3.0 have none, they end at their first check
            // (everyone logs in again once).
            migrationBuilder.Sql(
                "UPDATE accounts SET username_key = lower(username), stamp = md5(random()::text || id::text);");

            migrationBuilder.CreateIndex(
                name: "IX_accounts_username_key",
                table: "accounts",
                column: "username_key",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_accounts_username_key",
                table: "accounts");

            migrationBuilder.DropColumn(
                name: "disabled",
                table: "accounts");

            migrationBuilder.DropColumn(
                name: "display_name",
                table: "accounts");

            migrationBuilder.DropColumn(
                name: "role",
                table: "accounts");

            migrationBuilder.DropColumn(
                name: "source",
                table: "accounts");

            migrationBuilder.DropColumn(
                name: "stamp",
                table: "accounts");

            migrationBuilder.DropColumn(
                name: "username_key",
                table: "accounts");

            migrationBuilder.AlterColumn<string>(
                name: "password_hash",
                table: "accounts",
                type: "text",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);
        }
    }
}
