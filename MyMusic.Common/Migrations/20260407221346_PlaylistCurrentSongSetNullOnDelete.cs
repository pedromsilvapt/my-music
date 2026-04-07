using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyMusic.Common.Migrations
{
    /// <inheritdoc />
    public partial class PlaylistCurrentSongSetNullOnDelete : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_playlists_songs_current_song_id",
                table: "playlists");

            migrationBuilder.AddForeignKey(
                name: "fk_playlists_songs_current_song_id",
                table: "playlists",
                column: "current_song_id",
                principalTable: "songs",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_playlists_songs_current_song_id",
                table: "playlists");

            migrationBuilder.AddForeignKey(
                name: "fk_playlists_songs_current_song_id",
                table: "playlists",
                column: "current_song_id",
                principalTable: "songs",
                principalColumn: "id");
        }
    }
}
