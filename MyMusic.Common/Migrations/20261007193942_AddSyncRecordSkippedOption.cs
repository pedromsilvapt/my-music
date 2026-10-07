using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyMusic.Common.Migrations
{
    /// <inheritdoc />
    public partial class AddSyncRecordSkippedOption : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "deleted_skipped_count",
                table: "device_sync_sessions",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "record_skipped",
                table: "device_sync_sessions",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            // Sessions that already exist still have their Skipped records
            migrationBuilder.Sql("UPDATE device_sync_sessions SET record_skipped = TRUE;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "deleted_skipped_count",
                table: "device_sync_sessions");

            migrationBuilder.DropColumn(
                name: "record_skipped",
                table: "device_sync_sessions");
        }
    }
}
