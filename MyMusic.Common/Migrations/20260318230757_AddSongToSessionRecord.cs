using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyMusic.Common.Migrations
{
    /// <inheritdoc />
    public partial class AddSongToSessionRecord : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "ix_device_sync_session_records_song_id",
                table: "device_sync_session_records",
                column: "song_id");

            migrationBuilder.AddForeignKey(
                name: "fk_device_sync_session_records_songs_song_id",
                table: "device_sync_session_records",
                column: "song_id",
                principalTable: "songs",
                principalColumn: "id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_device_sync_session_records_songs_song_id",
                table: "device_sync_session_records");

            migrationBuilder.DropIndex(
                name: "ix_device_sync_session_records_song_id",
                table: "device_sync_session_records");
        }
    }
}
