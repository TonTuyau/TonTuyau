using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TonTuyau.Infrastructure.Accounts.Migrations
{
    /// <inheritdoc />
    public partial class GoneVideosToRetryLater : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1.5.0-alpha.6: "gone" no longer includes private videos, and a gone video isn't read again. What was
            // found gone before is checked again by the next refresh, with the new rules.
            migrationBuilder.Sql("UPDATE youtube_videos SET state = 'RetryLater' WHERE state = 'Gone';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Nothing to undo: which of them were gone isn't known any more, and retry later is always safe.
        }
    }
}
