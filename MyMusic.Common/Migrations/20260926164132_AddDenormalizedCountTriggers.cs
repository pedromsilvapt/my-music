using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyMusic.Common.Migrations
{
    /// <inheritdoc />
    public partial class AddDenormalizedCountTriggers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<int>(
                name: "songs_count",
                table: "artists",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AlterColumn<int>(
                name: "albums_count",
                table: "artists",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AlterColumn<int>(
                name: "songs_count",
                table: "albums",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "integer");

            // Denormalized counts (albums.songs_count, artists.songs_count, artists.albums_count)
            // are maintained exclusively by these row-level triggers. Row triggers also fire for
            // bulk ExecuteDelete statements and FK cascades, so every mutation path is covered.
            migrationBuilder.Sql(@"
CREATE OR REPLACE FUNCTION fn_song_album_counts()
RETURNS TRIGGER
LANGUAGE plpgsql
AS $$
BEGIN
    IF TG_OP IN ('DELETE', 'UPDATE') THEN
        UPDATE albums SET songs_count = songs_count - 1 WHERE id = OLD.album_id;
    END IF;
    IF TG_OP IN ('INSERT', 'UPDATE') THEN
        UPDATE albums SET songs_count = songs_count + 1 WHERE id = NEW.album_id;
    END IF;
    RETURN NULL;
END;
$$;

CREATE OR REPLACE FUNCTION fn_song_artist_counts()
RETURNS TRIGGER
LANGUAGE plpgsql
AS $$
BEGIN
    IF TG_OP IN ('DELETE', 'UPDATE') THEN
        UPDATE artists SET songs_count = songs_count - 1 WHERE id = OLD.artist_id;
    END IF;
    IF TG_OP IN ('INSERT', 'UPDATE') THEN
        UPDATE artists SET songs_count = songs_count + 1 WHERE id = NEW.artist_id;
    END IF;
    RETURN NULL;
END;
$$;

CREATE OR REPLACE FUNCTION fn_album_artist_counts()
RETURNS TRIGGER
LANGUAGE plpgsql
AS $$
BEGIN
    IF TG_OP IN ('DELETE', 'UPDATE') THEN
        UPDATE artists SET albums_count = albums_count - 1 WHERE id = OLD.artist_id;
    END IF;
    IF TG_OP IN ('INSERT', 'UPDATE') THEN
        UPDATE artists SET albums_count = albums_count + 1 WHERE id = NEW.artist_id;
    END IF;
    RETURN NULL;
END;
$$;

CREATE TRIGGER tr_songs_album_counts_insert
    AFTER INSERT ON songs
    FOR EACH ROW EXECUTE FUNCTION fn_song_album_counts();
CREATE TRIGGER tr_songs_album_counts_delete
    AFTER DELETE ON songs
    FOR EACH ROW EXECUTE FUNCTION fn_song_album_counts();
CREATE TRIGGER tr_songs_album_counts_update
    AFTER UPDATE OF album_id ON songs
    FOR EACH ROW WHEN (OLD.album_id IS DISTINCT FROM NEW.album_id)
    EXECUTE FUNCTION fn_song_album_counts();

CREATE TRIGGER tr_song_artists_artist_counts_insert
    AFTER INSERT ON song_artists
    FOR EACH ROW EXECUTE FUNCTION fn_song_artist_counts();
CREATE TRIGGER tr_song_artists_artist_counts_delete
    AFTER DELETE ON song_artists
    FOR EACH ROW EXECUTE FUNCTION fn_song_artist_counts();
CREATE TRIGGER tr_song_artists_artist_counts_update
    AFTER UPDATE OF artist_id ON song_artists
    FOR EACH ROW WHEN (OLD.artist_id IS DISTINCT FROM NEW.artist_id)
    EXECUTE FUNCTION fn_song_artist_counts();

CREATE TRIGGER tr_albums_artist_counts_insert
    AFTER INSERT ON albums
    FOR EACH ROW EXECUTE FUNCTION fn_album_artist_counts();
CREATE TRIGGER tr_albums_artist_counts_delete
    AFTER DELETE ON albums
    FOR EACH ROW EXECUTE FUNCTION fn_album_artist_counts();
CREATE TRIGGER tr_albums_artist_counts_update
    AFTER UPDATE OF artist_id ON albums
    FOR EACH ROW WHEN (OLD.artist_id IS DISTINCT FROM NEW.artist_id)
    EXECUTE FUNCTION fn_album_artist_counts();
");

            // Recalculate every count so the triggers start from a correct baseline.
            migrationBuilder.Sql(@"
UPDATE albums
SET songs_count = (SELECT COUNT(*) FROM songs WHERE songs.album_id = albums.id);

UPDATE artists
SET songs_count = (SELECT COUNT(*) FROM song_artists WHERE song_artists.artist_id = artists.id),
    albums_count = (SELECT COUNT(*) FROM albums WHERE albums.artist_id = artists.id);
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
DROP TRIGGER IF EXISTS tr_songs_album_counts_insert ON songs;
DROP TRIGGER IF EXISTS tr_songs_album_counts_delete ON songs;
DROP TRIGGER IF EXISTS tr_songs_album_counts_update ON songs;
DROP TRIGGER IF EXISTS tr_song_artists_artist_counts_insert ON song_artists;
DROP TRIGGER IF EXISTS tr_song_artists_artist_counts_delete ON song_artists;
DROP TRIGGER IF EXISTS tr_song_artists_artist_counts_update ON song_artists;
DROP TRIGGER IF EXISTS tr_albums_artist_counts_insert ON albums;
DROP TRIGGER IF EXISTS tr_albums_artist_counts_delete ON albums;
DROP TRIGGER IF EXISTS tr_albums_artist_counts_update ON albums;

DROP FUNCTION IF EXISTS fn_song_album_counts();
DROP FUNCTION IF EXISTS fn_song_artist_counts();
DROP FUNCTION IF EXISTS fn_album_artist_counts();
");

            migrationBuilder.AlterColumn<int>(
                name: "songs_count",
                table: "artists",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer",
                oldDefaultValue: 0);

            migrationBuilder.AlterColumn<int>(
                name: "albums_count",
                table: "artists",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer",
                oldDefaultValue: 0);

            migrationBuilder.AlterColumn<int>(
                name: "songs_count",
                table: "albums",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer",
                oldDefaultValue: 0);
        }
    }
}
