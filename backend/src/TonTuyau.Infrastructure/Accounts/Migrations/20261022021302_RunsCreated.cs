using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TonTuyau.Infrastructure.Accounts.Migrations
{
    /// <inheritdoc />
    public partial class RunsCreated : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "created",
                table: "runs",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            // The runs already there: their new videos, from their JSON (the history counts the downloads' too).
            migrationBuilder.Sql(
                "UPDATE runs SET created = COALESCE((run -> 'counts' ->> 'created')::integer, 0);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "created",
                table: "runs");
        }
    }
}
