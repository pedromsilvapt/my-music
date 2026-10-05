using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyMusic.Common.Migrations
{
    /// <inheritdoc />
    public partial class ChangeSongHistoryPrincipalTriggersToBefore : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // A queued snapshot is the song as it was before the change. These triggers ran after the album, artist
            // or genre was renamed, so their snapshots already carried the new name and the rename never showed up
            // in the song's history. Like the triggers on songs, they now run before the row changes.
            migrationBuilder.Sql(@"
DROP TRIGGER IF EXISTS trg_song_history_on_album_update ON albums;
CREATE TRIGGER trg_song_history_on_album_update
    BEFORE UPDATE ON albums
    FOR EACH ROW
    WHEN (NEW.name IS DISTINCT FROM OLD.name OR NEW.artist_id IS DISTINCT FROM OLD.artist_id)
    EXECUTE FUNCTION fn_song_history_on_album_update();

DROP TRIGGER IF EXISTS trg_song_history_on_artist_update ON artists;
CREATE TRIGGER trg_song_history_on_artist_update
    BEFORE UPDATE ON artists
    FOR EACH ROW
    WHEN (NEW.name IS DISTINCT FROM OLD.name)
    EXECUTE FUNCTION fn_song_history_on_artist_update();

DROP TRIGGER IF EXISTS trg_song_history_on_genre_update ON genres;
CREATE TRIGGER trg_song_history_on_genre_update
    BEFORE UPDATE ON genres
    FOR EACH ROW
    WHEN (NEW.name IS DISTINCT FROM OLD.name)
    EXECUTE FUNCTION fn_song_history_on_genre_update();
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
DROP TRIGGER IF EXISTS trg_song_history_on_album_update ON albums;
CREATE TRIGGER trg_song_history_on_album_update
    AFTER UPDATE ON albums
    FOR EACH ROW
    WHEN (NEW.name IS DISTINCT FROM OLD.name OR NEW.artist_id IS DISTINCT FROM OLD.artist_id)
    EXECUTE FUNCTION fn_song_history_on_album_update();

DROP TRIGGER IF EXISTS trg_song_history_on_artist_update ON artists;
CREATE TRIGGER trg_song_history_on_artist_update
    AFTER UPDATE ON artists
    FOR EACH ROW
    WHEN (NEW.name IS DISTINCT FROM OLD.name)
    EXECUTE FUNCTION fn_song_history_on_artist_update();

DROP TRIGGER IF EXISTS trg_song_history_on_genre_update ON genres;
CREATE TRIGGER trg_song_history_on_genre_update
    AFTER UPDATE ON genres
    FOR EACH ROW
    WHEN (NEW.name IS DISTINCT FROM OLD.name)
    EXECUTE FUNCTION fn_song_history_on_genre_update();
");
        }
    }
}
