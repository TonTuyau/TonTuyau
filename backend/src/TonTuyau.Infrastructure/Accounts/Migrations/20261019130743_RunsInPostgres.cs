using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TonTuyau.Infrastructure.Accounts.Migrations
{
    /// <inheritdoc />
    public partial class RunsInPostgres : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "pending_art",
                columns: table => new
                {
                    path = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_pending_art", x => x.path);
                });

            migrationBuilder.CreateTable(
                name: "runs",
                columns: table => new
                {
                    id = table.Column<string>(type: "text", nullable: false),
                    mode = table.Column<string>(type: "text", nullable: false),
                    trigger = table.Column<string>(type: "text", nullable: false),
                    outcome = table.Column<string>(type: "text", nullable: false),
                    started_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    finished_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    run = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_runs", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "sync_lock",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false),
                    owner = table.Column<string>(type: "text", nullable: false),
                    mode = table.Column<string>(type: "text", nullable: false),
                    trigger = table.Column<string>(type: "text", nullable: false),
                    acquired_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    lease_until = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sync_lock", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_runs_mode_started_at",
                table: "runs",
                columns: new[] { "mode", "started_at" });

            migrationBuilder.CreateIndex(
                name: "IX_runs_outcome",
                table: "runs",
                column: "outcome");

            migrationBuilder.CreateIndex(
                name: "IX_runs_started_at_id",
                table: "runs",
                columns: new[] { "started_at", "id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "pending_art");

            migrationBuilder.DropTable(
                name: "runs");

            migrationBuilder.DropTable(
                name: "sync_lock");
        }
    }
}
