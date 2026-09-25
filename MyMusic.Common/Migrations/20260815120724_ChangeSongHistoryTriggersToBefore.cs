using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyMusic.Common.Migrations
{
    /// <inheritdoc />
    public partial class ChangeSongHistoryTriggersToBefore : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "diff_format",
                table: "song_histories",
                type: "text",
                nullable: false,
                defaultValue: "snapshot");

            migrationBuilder.Sql(@"
DROP TRIGGER IF EXISTS trg_song_history_on_update ON songs;
CREATE TRIGGER trg_song_history_on_update
    BEFORE UPDATE ON songs
    FOR EACH ROW
    EXECUTE FUNCTION fn_song_history_on_update();

DROP TRIGGER IF EXISTS trg_song_history_on_artist_insert ON song_artists;
CREATE TRIGGER trg_song_history_on_artist_insert
    BEFORE INSERT ON song_artists
    FOR EACH ROW
    EXECUTE FUNCTION fn_song_history_on_artist_insert();

DROP TRIGGER IF EXISTS trg_song_history_on_artist_delete ON song_artists;
CREATE TRIGGER trg_song_history_on_artist_delete
    BEFORE DELETE ON song_artists
    FOR EACH ROW
    EXECUTE FUNCTION fn_song_history_on_artist_delete();

DROP TRIGGER IF EXISTS trg_song_history_on_genre_insert ON song_genres;
CREATE TRIGGER trg_song_history_on_genre_insert
    BEFORE INSERT ON song_genres
    FOR EACH ROW
    EXECUTE FUNCTION fn_song_history_on_genre_insert();

DROP TRIGGER IF EXISTS trg_song_history_on_genre_delete ON song_genres;
CREATE TRIGGER trg_song_history_on_genre_delete
    BEFORE DELETE ON song_genres
    FOR EACH ROW
    EXECUTE FUNCTION fn_song_history_on_genre_delete();

DROP TRIGGER IF EXISTS trg_song_history_on_source_insert ON song_sources;
CREATE TRIGGER trg_song_history_on_source_insert
    BEFORE INSERT ON song_sources
    FOR EACH ROW
    EXECUTE FUNCTION fn_song_history_on_source_insert();

DROP TRIGGER IF EXISTS trg_song_history_on_source_delete ON song_sources;
CREATE TRIGGER trg_song_history_on_source_delete
    BEFORE DELETE ON song_sources
    FOR EACH ROW
    EXECUTE FUNCTION fn_song_history_on_source_delete();

DROP TRIGGER IF EXISTS trg_song_history_on_device_insert ON song_devices;
CREATE TRIGGER trg_song_history_on_device_insert
    BEFORE INSERT ON song_devices
    FOR EACH ROW
    EXECUTE FUNCTION fn_song_history_on_device_insert();

DROP TRIGGER IF EXISTS trg_song_history_on_device_delete ON song_devices;
CREATE TRIGGER trg_song_history_on_device_delete
    BEFORE DELETE ON song_devices
    FOR EACH ROW
    EXECUTE FUNCTION fn_song_history_on_device_delete();

DROP TRIGGER IF EXISTS trg_song_history_on_device_update ON song_devices;
CREATE TRIGGER trg_song_history_on_device_update
    BEFORE UPDATE ON song_devices
    FOR EACH ROW
    EXECUTE FUNCTION fn_song_history_on_device_update();
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
DROP TRIGGER IF EXISTS trg_song_history_on_update ON songs;
CREATE TRIGGER trg_song_history_on_update
    AFTER UPDATE ON songs
    FOR EACH ROW
    EXECUTE FUNCTION fn_song_history_on_update();

DROP TRIGGER IF EXISTS trg_song_history_on_artist_insert ON song_artists;
CREATE TRIGGER trg_song_history_on_artist_insert
    AFTER INSERT ON song_artists
    FOR EACH ROW
    EXECUTE FUNCTION fn_song_history_on_artist_insert();

DROP TRIGGER IF EXISTS trg_song_history_on_artist_delete ON song_artists;
CREATE TRIGGER trg_song_history_on_artist_delete
    AFTER DELETE ON song_artists
    FOR EACH ROW
    EXECUTE FUNCTION fn_song_history_on_artist_delete();

DROP TRIGGER IF EXISTS trg_song_history_on_genre_insert ON song_genres;
CREATE TRIGGER trg_song_history_on_genre_insert
    AFTER INSERT ON song_genres
    FOR EACH ROW
    EXECUTE FUNCTION fn_song_history_on_genre_insert();

DROP TRIGGER IF EXISTS trg_song_history_on_genre_delete ON song_genres;
CREATE TRIGGER trg_song_history_on_genre_delete
    AFTER DELETE ON song_genres
    FOR EACH ROW
    EXECUTE FUNCTION fn_song_history_on_genre_delete();

DROP TRIGGER IF EXISTS trg_song_history_on_source_insert ON song_sources;
CREATE TRIGGER trg_song_history_on_source_insert
    AFTER INSERT ON song_sources
    FOR EACH ROW
    EXECUTE FUNCTION fn_song_history_on_source_insert();

DROP TRIGGER IF EXISTS trg_song_history_on_source_delete ON song_sources;
CREATE TRIGGER trg_song_history_on_source_delete
    AFTER DELETE ON song_sources
    FOR EACH ROW
    EXECUTE FUNCTION fn_song_history_on_source_delete();

DROP TRIGGER IF EXISTS trg_song_history_on_device_insert ON song_devices;
CREATE TRIGGER trg_song_history_on_device_insert
    AFTER INSERT ON song_devices
    FOR EACH ROW
    EXECUTE FUNCTION fn_song_history_on_device_insert();

DROP TRIGGER IF EXISTS trg_song_history_on_device_delete ON song_devices;
CREATE TRIGGER trg_song_history_on_device_delete
    AFTER DELETE ON song_devices
    FOR EACH ROW
    EXECUTE FUNCTION fn_song_history_on_device_delete();

DROP TRIGGER IF EXISTS trg_song_history_on_device_update ON song_devices;
CREATE TRIGGER trg_song_history_on_device_update
    AFTER UPDATE ON song_devices
    FOR EACH ROW
    EXECUTE FUNCTION fn_song_history_on_device_update();
");

            migrationBuilder.DropColumn(
                name: "diff_format",
                table: "song_histories");
        }
    }
}
