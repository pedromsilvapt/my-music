using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyMusic.Common.Migrations
{
    /// <inheritdoc />
    public partial class RenameSkipAfterPlaybackToSkipNextPlayback : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "skip_after_playback",
                table: "playlist_songs",
                newName: "skip_next_playback");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "skip_next_playback",
                table: "playlist_songs",
                newName: "skip_after_playback");
        }
    }
}
