using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyMusic.Common.Migrations
{
    /// <inheritdoc />
    public partial class AddSyncSessionStatusCommitted : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                UPDATE device_sync_sessions
                SET status = status + 1
                WHERE status >= 1;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                DELETE FROM device_sync_sessions
                WHERE status = 1;

                UPDATE device_sync_sessions
                SET status = status - 1
                WHERE status >= 2;
                """);
        }
    }
}