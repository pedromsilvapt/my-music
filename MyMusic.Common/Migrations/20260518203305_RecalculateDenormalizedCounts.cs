using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyMusic.Common.Migrations
{
    /// <inheritdoc />
    public partial class RecalculateDenormalizedCounts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                UPDATE albums
                SET songs_count = (SELECT COUNT(*) FROM songs WHERE songs.album_id = albums.id)
            """);

            migrationBuilder.Sql("""
                UPDATE artists
                SET songs_count = (SELECT COUNT(*) FROM song_artists WHERE song_artists.artist_id = artists.id)
            """);

            migrationBuilder.Sql("""
                UPDATE artists
                SET albums_count = (SELECT COUNT(*) FROM albums WHERE albums.artist_id = artists.id)
            """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
        }
    }
}
