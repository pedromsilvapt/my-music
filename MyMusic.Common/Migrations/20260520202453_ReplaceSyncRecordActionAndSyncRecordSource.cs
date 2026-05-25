using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyMusic.Common.Migrations
{
    /// <inheritdoc />
    public partial class ReplaceSyncRecordActionAndSyncRecordSource : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_device_sync_session_records_session_id_file_path",
                table: "device_sync_session_records");

            migrationBuilder.Sql("DELETE FROM device_sync_session_records");

            migrationBuilder.DropColumn(
                name: "error_message",
                table: "device_sync_session_records");

            migrationBuilder.DropColumn(
                name: "source",
                table: "device_sync_session_records");

            migrationBuilder.AddColumn<string>(
                name: "data",
                table: "device_sync_session_records",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "resolves_conflict_record_id",
                table: "device_sync_session_records",
                type: "bigint",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_device_sync_session_records_resolves_conflict_record_id",
                table: "device_sync_session_records",
                column: "resolves_conflict_record_id");

            migrationBuilder.CreateIndex(
                name: "ix_device_sync_session_records_session_id",
                table: "device_sync_session_records",
                column: "session_id");

            migrationBuilder.AddForeignKey(
                name: "fk_device_sync_session_records_device_sync_session_records_res",
                table: "device_sync_session_records",
                column: "resolves_conflict_record_id",
                principalTable: "device_sync_session_records",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_device_sync_session_records_device_sync_session_records_res",
                table: "device_sync_session_records");

            migrationBuilder.DropIndex(
                name: "ix_device_sync_session_records_resolves_conflict_record_id",
                table: "device_sync_session_records");

            migrationBuilder.DropIndex(
                name: "ix_device_sync_session_records_session_id",
                table: "device_sync_session_records");

            migrationBuilder.DropColumn(
                name: "data",
                table: "device_sync_session_records");

            migrationBuilder.DropColumn(
                name: "resolves_conflict_record_id",
                table: "device_sync_session_records");

            migrationBuilder.AddColumn<string>(
                name: "error_message",
                table: "device_sync_session_records",
                type: "character varying(2048)",
                maxLength: 2048,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "source",
                table: "device_sync_session_records",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "ix_device_sync_session_records_session_id_file_path",
                table: "device_sync_session_records",
                columns: new[] { "session_id", "file_path" },
                unique: true);
        }
    }
}