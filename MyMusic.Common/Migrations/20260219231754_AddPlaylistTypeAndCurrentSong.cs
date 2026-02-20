using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyMusic.Common.Migrations
{
    /// <inheritdoc />
    public partial class AddPlaylistTypeAndCurrentSong : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "current_song_id",
                table: "playlists",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "type",
                table: "playlists",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "ix_playlists_current_song_id",
                table: "playlists",
                column: "current_song_id");

            migrationBuilder.AddForeignKey(
                name: "fk_playlists_songs_current_song_id",
                table: "playlists",
                column: "current_song_id",
                principalTable: "songs",
                principalColumn: "id");

            migrationBuilder.Sql("""
                INSERT INTO playlists (name, type, owner_id, created_at, modified_at)
                SELECT 'Queue', 1, id, NOW(), NOW()
                FROM users
                WHERE NOT EXISTS (SELECT 1 FROM playlists WHERE type = 1 AND owner_id = users.id)
                """);

            migrationBuilder.Sql("""
                INSERT INTO playlists (name, type, owner_id, created_at, modified_at)
                SELECT 'Favorites', 2, id, NOW(), NOW()
                FROM users
                WHERE NOT EXISTS (SELECT 1 FROM playlists WHERE type = 2 AND owner_id = users.id)
                """);

            migrationBuilder.Sql("""
                INSERT INTO playlist_songs (playlist_id, song_id, "order", added_at)
                SELECT 
                    p.id,
                    s.id,
                    ROW_NUMBER() OVER (PARTITION BY s.owner_id ORDER BY s.id) - 1,
                    NOW()
                FROM songs s
                INNER JOIN playlists p ON p.owner_id = s.owner_id AND p.type = 2
                WHERE s.is_favorite = true
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DELETE FROM playlist_songs WHERE playlist_id IN (SELECT id FROM playlists WHERE type IN (1, 2))");
            migrationBuilder.Sql("DELETE FROM playlists WHERE type IN (1, 2)");

            migrationBuilder.DropForeignKey(
                name: "fk_playlists_songs_current_song_id",
                table: "playlists");

            migrationBuilder.DropIndex(
                name: "ix_playlists_current_song_id",
                table: "playlists");

            migrationBuilder.DropColumn(
                name: "current_song_id",
                table: "playlists");

            migrationBuilder.DropColumn(
                name: "type",
                table: "playlists");
        }
    }
}
