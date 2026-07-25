using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyMusic.Common.Migrations
{
    /// <inheritdoc />
    public partial class AddDeviceSyncSessionRecordSessionIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_device_sync_session_records_session_id",
                table: "device_sync_session_records");

            migrationBuilder.CreateIndex(
                name: "ix_device_sync_session_records_session_id_file_path",
                table: "device_sync_session_records",
                columns: new[] { "session_id", "file_path" });

            migrationBuilder.CreateIndex(
                name: "ix_device_sync_session_records_session_id_song_id",
                table: "device_sync_session_records",
                columns: new[] { "session_id", "song_id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_device_sync_session_records_session_id_file_path",
                table: "device_sync_session_records");

            migrationBuilder.DropIndex(
                name: "ix_device_sync_session_records_session_id_song_id",
                table: "device_sync_session_records");

            migrationBuilder.CreateIndex(
                name: "ix_device_sync_session_records_session_id",
                table: "device_sync_session_records",
                column: "session_id");
        }
    }
}
