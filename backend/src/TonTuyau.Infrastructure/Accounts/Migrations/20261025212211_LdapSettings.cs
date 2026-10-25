using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TonTuyau.Infrastructure.Accounts.Migrations
{
    /// <inheritdoc />
    public partial class LdapSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ldap_settings",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false),
                    enabled = table.Column<bool>(type: "boolean", nullable: false),
                    url = table.Column<string>(type: "text", nullable: false),
                    start_tls = table.Column<bool>(type: "boolean", nullable: false),
                    ca_certificate = table.Column<string>(type: "text", nullable: true),
                    bind_dn = table.Column<string>(type: "text", nullable: false),
                    bind_password = table.Column<string>(type: "text", nullable: true),
                    base_dn = table.Column<string>(type: "text", nullable: false),
                    uid_attribute = table.Column<string>(type: "text", nullable: false),
                    username_attribute = table.Column<string>(type: "text", nullable: false),
                    users_filter = table.Column<string>(type: "text", nullable: false),
                    admins_filter = table.Column<string>(type: "text", nullable: false),
                    password_attribute = table.Column<string>(type: "text", nullable: false),
                    password_reset_url = table.Column<string>(type: "text", nullable: true),
                    changed_by = table.Column<string>(type: "text", nullable: true),
                    changed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ldap_settings", x => x.id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ldap_settings");
        }
    }
}
