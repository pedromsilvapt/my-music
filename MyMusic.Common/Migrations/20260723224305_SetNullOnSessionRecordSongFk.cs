using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyMusic.Common.Migrations
{
    /// <inheritdoc />
    public partial class SetNullOnSessionRecordSongFk : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_device_sync_session_records_songs_song_id",
                table: "device_sync_session_records");

            migrationBuilder.AddForeignKey(
                name: "fk_device_sync_session_records_songs_song_id",
                table: "device_sync_session_records",
                column: "song_id",
                principalTable: "songs",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_device_sync_session_records_songs_song_id",
                table: "device_sync_session_records");

            migrationBuilder.AddForeignKey(
                name: "fk_device_sync_session_records_songs_song_id",
                table: "device_sync_session_records",
                column: "song_id",
                principalTable: "songs",
                principalColumn: "id");
        }
    }
}
