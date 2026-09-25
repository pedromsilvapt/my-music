using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace MyMusic.Common.Migrations
{
    /// <inheritdoc />
    public partial class AddSongHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "song_histories",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    song_id = table.Column<long>(type: "bigint", nullable: false),
                    song_revision = table.Column<int>(type: "integer", nullable: false),
                    diff = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_song_histories", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "song_history_queues",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    song_id = table.Column<long>(type: "bigint", nullable: false),
                    song_revision = table.Column<int>(type: "integer", nullable: false),
                    data = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    processed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    error_count = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    last_error = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_song_history_queues", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_song_histories_song_id_song_revision",
                table: "song_histories",
                columns: new[] { "song_id", "song_revision" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_song_history_queues_processed_at",
                table: "song_history_queues",
                column: "processed_at");

            migrationBuilder.CreateIndex(
                name: "ix_song_history_queues_song_id",
                table: "song_history_queues",
                column: "song_id");

            migrationBuilder.CreateIndex(
                name: "ix_song_history_queues_song_id_song_revision",
                table: "song_history_queues",
                columns: new[] { "song_id", "song_revision" },
                unique: true);

            // --- Song History Tracking: trigger functions and triggers ---
            //
            // The next two helper functions are LANGUAGE sql:
            //   * next_song_revision(bigint)            -> next monotonic revision for a song
            //   * song_history_build_snapshot(bigint, boolean, text) -> full JSONB snapshot
            //
            // song_history_build_snapshot centralizes the snapshot shape so every trigger
            // (songs update/delete + all join-table triggers) stores the identical JSON.
            // p_include_cover controls whether the (potentially large, base64-encoded)
            // cover artwork is embedded; p_action is the 'action' value stored in the JSON.

            migrationBuilder.Sql(@"
CREATE OR REPLACE FUNCTION next_song_revision(p_song_id bigint)
RETURNS integer
LANGUAGE sql
AS $$
    SELECT COALESCE(MAX(song_revision), 0) + 1
    FROM (
        SELECT song_revision FROM song_history_queues
        WHERE song_id = p_song_id AND processed_at IS NULL
        UNION ALL
        SELECT song_revision FROM song_histories
        WHERE song_id = p_song_id
    ) t
$$;

CREATE OR REPLACE FUNCTION song_history_build_snapshot(
    p_song_id bigint,
    p_include_cover boolean,
    p_action text
)
RETURNS jsonb
LANGUAGE sql
AS $$
    SELECT jsonb_build_object(
        'id', s.id,
        'title', s.title,
        'label', s.label,
        'album_id', s.album_id,
        'cover_id', s.cover_id,
        'year', s.year,
        'lyrics', s.lyrics,
        'explicit', s.explicit,
        'size', s.size,
        'track', s.track,
        'duration', s.duration,
        'bitrate', s.bitrate,
        'owner_id', s.owner_id,
        'rating', s.rating,
        'is_favorite', s.is_favorite,
        'play_count', s.play_count,
        'repository_path', s.repository_path,
        'checksum', s.checksum,
        'checksum_algorithm', s.checksum_algorithm,
        'added_at', s.added_at,
        'created_at', s.created_at,
        'modified_at', s.modified_at,
        'file_modified_at', s.file_modified_at,
        'action', p_action,
        'album',
            (SELECT jsonb_build_object('id', a.id, 'title', a.name)
             FROM albums a WHERE a.id = s.album_id),
        'artists',
            COALESCE((SELECT jsonb_agg(jsonb_build_object('id', ar.id, 'name', ar.name))
                      FROM song_artists sa
                      JOIN artists ar ON ar.id = sa.artist_id
                      WHERE sa.song_id = s.id), '[]'::jsonb),
        'genres',
            COALESCE((SELECT jsonb_agg(jsonb_build_object('id', g.id, 'name', g.name))
                      FROM song_genres sg
                      JOIN genres g ON g.id = sg.genre_id
                      WHERE sg.song_id = s.id), '[]'::jsonb),
        'sources',
            COALESCE((SELECT jsonb_agg(jsonb_build_object('id', src.id, 'name', src.name))
                      FROM song_sources ss
                      JOIN sources src ON src.id = ss.source_id
                      WHERE ss.song_id = s.id), '[]'::jsonb),
        'devices',
            COALESCE((SELECT jsonb_agg(jsonb_build_object('id', sd.id, 'device_path', sd.device_path, 'sync_action', sd.sync_action))
                      FROM song_devices sd
                      WHERE sd.song_id = s.id), '[]'::jsonb)
    ) || CASE
        WHEN p_include_cover AND s.cover_id IS NOT NULL THEN
            jsonb_build_object('cover',
                (SELECT jsonb_build_object(
                    'id', aw.id,
                    'mime_type', aw.mime_type,
                    'width', aw.width,
                    'height', aw.height,
                    'data', encode(aw.data, 'base64'))
                 FROM artworks aw WHERE aw.id = s.cover_id))
        ELSE '{}'::jsonb
    END
    FROM songs s
    WHERE s.id = p_song_id
$$;
");

            // songs AFTER UPDATE: enqueue a snapshot only when a tracked field changed.
            // Tracked fields = all columns EXCEPT modified_at and file_modified_at.
            migrationBuilder.Sql(@"
CREATE OR REPLACE FUNCTION fn_song_history_on_update()
RETURNS TRIGGER
LANGUAGE plpgsql
AS $$
BEGIN
    IF
        NEW.id IS DISTINCT FROM OLD.id
        OR NEW.title IS DISTINCT FROM OLD.title
        OR NEW.label IS DISTINCT FROM OLD.label
        OR NEW.album_id IS DISTINCT FROM OLD.album_id
        OR NEW.cover_id IS DISTINCT FROM OLD.cover_id
        OR NEW.year IS DISTINCT FROM OLD.year
        OR NEW.lyrics IS DISTINCT FROM OLD.lyrics
        OR NEW.explicit IS DISTINCT FROM OLD.explicit
        OR NEW.size IS DISTINCT FROM OLD.size
        OR NEW.track IS DISTINCT FROM OLD.track
        OR NEW.duration IS DISTINCT FROM OLD.duration
        OR NEW.bitrate IS DISTINCT FROM OLD.bitrate
        OR NEW.owner_id IS DISTINCT FROM OLD.owner_id
        OR NEW.rating IS DISTINCT FROM OLD.rating
        OR NEW.is_favorite IS DISTINCT FROM OLD.is_favorite
        OR NEW.play_count IS DISTINCT FROM OLD.play_count
        OR NEW.repository_path IS DISTINCT FROM OLD.repository_path
        OR NEW.checksum IS DISTINCT FROM OLD.checksum
        OR NEW.checksum_algorithm IS DISTINCT FROM OLD.checksum_algorithm
        OR NEW.added_at IS DISTINCT FROM OLD.added_at
        OR NEW.created_at IS DISTINCT FROM OLD.created_at
    THEN
        INSERT INTO song_history_queues (song_id, song_revision, data, created_at)
        VALUES (
            NEW.id,
            next_song_revision(NEW.id),
            song_history_build_snapshot(NEW.id, (OLD.cover_id IS DISTINCT FROM NEW.cover_id), 'updated')::text,
            NOW()
        );
    END IF;
    RETURN NEW;
END;
$$;

DROP TRIGGER IF EXISTS trg_song_history_on_update ON songs;
CREATE TRIGGER trg_song_history_on_update
    AFTER UPDATE ON songs
    FOR EACH ROW
    EXECUTE FUNCTION fn_song_history_on_update();
");

            // songs BEFORE DELETE: enqueue a 'deleted' snapshot (cover included when present)
            // and BLOCK the deletion if any dead-lettered (error_count >= 3) pending queue
            // entries still exist for this song.
            migrationBuilder.Sql(@"
CREATE OR REPLACE FUNCTION fn_song_history_on_delete()
RETURNS TRIGGER
LANGUAGE plpgsql
AS $$
DECLARE
    v_dead_lettered integer;
BEGIN
    SELECT COUNT(*) INTO v_dead_lettered
    FROM song_history_queues
    WHERE song_id = OLD.id
      AND processed_at IS NULL
      AND error_count >= 3;

    IF v_dead_lettered > 0 THEN
        RAISE EXCEPTION
            'Cannot delete song %: it has % dead-lettered history queue entry/entries (error_count >= 3) still pending',
            OLD.id, v_dead_lettered;
    END IF;

    INSERT INTO song_history_queues (song_id, song_revision, data, created_at)
    VALUES (
        OLD.id,
        next_song_revision(OLD.id),
        song_history_build_snapshot(OLD.id, (OLD.cover_id IS NOT NULL), 'deleted')::text,
        NOW()
    );
    RETURN OLD;
END;
$$;

DROP TRIGGER IF EXISTS trg_song_history_on_delete ON songs;
CREATE TRIGGER trg_song_history_on_delete
    BEFORE DELETE ON songs
    FOR EACH ROW
    EXECUTE FUNCTION fn_song_history_on_delete();
");

            // song_artists AFTER INSERT / AFTER DELETE -> 'updated' snapshot from the affected song.
            migrationBuilder.Sql(@"
CREATE OR REPLACE FUNCTION fn_song_history_on_artist_insert()
RETURNS TRIGGER
LANGUAGE plpgsql
AS $$
BEGIN
    INSERT INTO song_history_queues (song_id, song_revision, data, created_at)
    VALUES (
        NEW.song_id,
        next_song_revision(NEW.song_id),
        song_history_build_snapshot(NEW.song_id, false, 'updated')::text,
        NOW()
    );
    RETURN NEW;
END;
$$;

CREATE OR REPLACE FUNCTION fn_song_history_on_artist_delete()
RETURNS TRIGGER
LANGUAGE plpgsql
AS $$
BEGIN
    INSERT INTO song_history_queues (song_id, song_revision, data, created_at)
    VALUES (
        OLD.song_id,
        next_song_revision(OLD.song_id),
        song_history_build_snapshot(OLD.song_id, false, 'updated')::text,
        NOW()
    );
    RETURN OLD;
END;
$$;

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
");

            // song_genres AFTER INSERT / AFTER DELETE.
            migrationBuilder.Sql(@"
CREATE OR REPLACE FUNCTION fn_song_history_on_genre_insert()
RETURNS TRIGGER
LANGUAGE plpgsql
AS $$
BEGIN
    INSERT INTO song_history_queues (song_id, song_revision, data, created_at)
    VALUES (
        NEW.song_id,
        next_song_revision(NEW.song_id),
        song_history_build_snapshot(NEW.song_id, false, 'updated')::text,
        NOW()
    );
    RETURN NEW;
END;
$$;

CREATE OR REPLACE FUNCTION fn_song_history_on_genre_delete()
RETURNS TRIGGER
LANGUAGE plpgsql
AS $$
BEGIN
    INSERT INTO song_history_queues (song_id, song_revision, data, created_at)
    VALUES (
        OLD.song_id,
        next_song_revision(OLD.song_id),
        song_history_build_snapshot(OLD.song_id, false, 'updated')::text,
        NOW()
    );
    RETURN OLD;
END;
$$;

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
");

            // song_sources AFTER INSERT / AFTER DELETE.
            migrationBuilder.Sql(@"
CREATE OR REPLACE FUNCTION fn_song_history_on_source_insert()
RETURNS TRIGGER
LANGUAGE plpgsql
AS $$
BEGIN
    INSERT INTO song_history_queues (song_id, song_revision, data, created_at)
    VALUES (
        NEW.song_id,
        next_song_revision(NEW.song_id),
        song_history_build_snapshot(NEW.song_id, false, 'updated')::text,
        NOW()
    );
    RETURN NEW;
END;
$$;

CREATE OR REPLACE FUNCTION fn_song_history_on_source_delete()
RETURNS TRIGGER
LANGUAGE plpgsql
AS $$
BEGIN
    INSERT INTO song_history_queues (song_id, song_revision, data, created_at)
    VALUES (
        OLD.song_id,
        next_song_revision(OLD.song_id),
        song_history_build_snapshot(OLD.song_id, false, 'updated')::text,
        NOW()
    );
    RETURN OLD;
END;
$$;

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
");

            // song_devices: AFTER INSERT, AFTER DELETE, and AFTER UPDATE only for the
            // SongDeleteService nullify case (OLD.song_id NOT NULL -> NEW.song_id NULL).
            migrationBuilder.Sql(@"
CREATE OR REPLACE FUNCTION fn_song_history_on_device_insert()
RETURNS TRIGGER
LANGUAGE plpgsql
AS $$
BEGIN
    INSERT INTO song_history_queues (song_id, song_revision, data, created_at)
    VALUES (
        NEW.song_id,
        next_song_revision(NEW.song_id),
        song_history_build_snapshot(NEW.song_id, false, 'updated')::text,
        NOW()
    );
    RETURN NEW;
END;
$$;

CREATE OR REPLACE FUNCTION fn_song_history_on_device_delete()
RETURNS TRIGGER
LANGUAGE plpgsql
AS $$
BEGIN
    INSERT INTO song_history_queues (song_id, song_revision, data, created_at)
    VALUES (
        OLD.song_id,
        next_song_revision(OLD.song_id),
        song_history_build_snapshot(OLD.song_id, false, 'updated')::text,
        NOW()
    );
    RETURN OLD;
END;
$$;

CREATE OR REPLACE FUNCTION fn_song_history_on_device_update()
RETURNS TRIGGER
LANGUAGE plpgsql
AS $$
BEGIN
    IF OLD.song_id IS NOT NULL AND NEW.song_id IS NULL THEN
        INSERT INTO song_history_queues (song_id, song_revision, data, created_at)
        VALUES (
            OLD.song_id,
            next_song_revision(OLD.song_id),
            song_history_build_snapshot(OLD.song_id, false, 'updated')::text,
            NOW()
        );
    END IF;
    RETURN NEW;
END;
$$;

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
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Drop triggers first, then trigger functions, then the shared helpers,
            // before EF drops the song_histories / song_history_queues tables.
            migrationBuilder.Sql(@"
DROP TRIGGER IF EXISTS trg_song_history_on_update ON songs;
DROP TRIGGER IF EXISTS trg_song_history_on_delete ON songs;
DROP TRIGGER IF EXISTS trg_song_history_on_artist_insert ON song_artists;
DROP TRIGGER IF EXISTS trg_song_history_on_artist_delete ON song_artists;
DROP TRIGGER IF EXISTS trg_song_history_on_genre_insert ON song_genres;
DROP TRIGGER IF EXISTS trg_song_history_on_genre_delete ON song_genres;
DROP TRIGGER IF EXISTS trg_song_history_on_source_insert ON song_sources;
DROP TRIGGER IF EXISTS trg_song_history_on_source_delete ON song_sources;
DROP TRIGGER IF EXISTS trg_song_history_on_device_insert ON song_devices;
DROP TRIGGER IF EXISTS trg_song_history_on_device_delete ON song_devices;
DROP TRIGGER IF EXISTS trg_song_history_on_device_update ON song_devices;

DROP FUNCTION IF EXISTS fn_song_history_on_update();
DROP FUNCTION IF EXISTS fn_song_history_on_delete();
DROP FUNCTION IF EXISTS fn_song_history_on_artist_insert();
DROP FUNCTION IF EXISTS fn_song_history_on_artist_delete();
DROP FUNCTION IF EXISTS fn_song_history_on_genre_insert();
DROP FUNCTION IF EXISTS fn_song_history_on_genre_delete();
DROP FUNCTION IF EXISTS fn_song_history_on_source_insert();
DROP FUNCTION IF EXISTS fn_song_history_on_source_delete();
DROP FUNCTION IF EXISTS fn_song_history_on_device_insert();
DROP FUNCTION IF EXISTS fn_song_history_on_device_delete();
DROP FUNCTION IF EXISTS fn_song_history_on_device_update();

DROP FUNCTION IF EXISTS song_history_build_snapshot(bigint, boolean, text);
DROP FUNCTION IF EXISTS next_song_revision(bigint);
");

            migrationBuilder.DropTable(
                name: "song_histories");

            migrationBuilder.DropTable(
                name: "song_history_queues");
        }
    }
}
