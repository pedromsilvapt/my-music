using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyMusic.Common.Migrations
{
    /// <inheritdoc />
    public partial class TrackAlbumArtistAndPrincipalUpdates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 2a. Replace song_history_build_snapshot so the album object also embeds the
            // album's principal artist (LEFT JOIN -> artist_id / artist_name).
            migrationBuilder.Sql(@"
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
            (SELECT jsonb_build_object(
                'id', a.id, 'title', a.name,
                'artist_id', a.artist_id,
                'artist_name', ar.name)
             FROM albums a
             LEFT JOIN artists ar ON ar.id = a.artist_id
             WHERE a.id = s.album_id),
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

            // 2b. Principal-table UPDATE triggers.
            //
            // These fan out to every affected song when a principal row (album/artist/genre)
            // changes. There is no soft-delete guard on these principal tables, so each
            // trigger unconditionally enqueues a snapshot for every affected song when the
            // relevant column IS DISTINCT FROM the old value.
            //
            // The enqueue shape matches the existing fn_song_history_on_* functions,
            // including the transaction_id column introduced in AddSongHistoryQueueTransactionId.

            // albums: fire on UPDATE of name OR artist_id; affected songs = songs of this album.
            migrationBuilder.Sql(@"
CREATE OR REPLACE FUNCTION fn_song_history_on_album_update()
RETURNS TRIGGER
LANGUAGE plpgsql
AS $$
DECLARE
    affected_id bigint;
BEGIN
    IF NEW.name IS DISTINCT FROM OLD.name
       OR NEW.artist_id IS DISTINCT FROM OLD.artist_id
    THEN
        FOR affected_id IN
            SELECT id FROM songs WHERE album_id = NEW.id
        LOOP
            INSERT INTO song_history_queues (song_id, song_revision, transaction_id, data, created_at)
            VALUES (
                affected_id,
                next_song_revision(affected_id),
                txid_current(),
                song_history_build_snapshot(affected_id, false, 'updated')::text,
                NOW()
            );
        END LOOP;
    END IF;
    RETURN NEW;
END;
$$;

DROP TRIGGER IF EXISTS trg_song_history_on_album_update ON albums;
CREATE TRIGGER trg_song_history_on_album_update
    AFTER UPDATE ON albums
    FOR EACH ROW
    WHEN (NEW.name IS DISTINCT FROM OLD.name OR NEW.artist_id IS DISTINCT FROM OLD.artist_id)
    EXECUTE FUNCTION fn_song_history_on_album_update();
");

            // artists: fire on UPDATE of name; affected songs = songs directly tagged with
            // this artist (song_artists) UNION songs whose album's artist is this artist.
            migrationBuilder.Sql(@"
CREATE OR REPLACE FUNCTION fn_song_history_on_artist_update()
RETURNS TRIGGER
LANGUAGE plpgsql
AS $$
DECLARE
    affected_id bigint;
BEGIN
    IF NEW.name IS DISTINCT FROM OLD.name
    THEN
        FOR affected_id IN
            SELECT sa.song_id FROM song_artists sa WHERE sa.artist_id = NEW.id
            UNION
            SELECT s.id FROM songs s JOIN albums a ON s.album_id = a.id WHERE a.artist_id = NEW.id
        LOOP
            INSERT INTO song_history_queues (song_id, song_revision, transaction_id, data, created_at)
            VALUES (
                affected_id,
                next_song_revision(affected_id),
                txid_current(),
                song_history_build_snapshot(affected_id, false, 'updated')::text,
                NOW()
            );
        END LOOP;
    END IF;
    RETURN NEW;
END;
$$;

DROP TRIGGER IF EXISTS trg_song_history_on_artist_update ON artists;
CREATE TRIGGER trg_song_history_on_artist_update
    AFTER UPDATE ON artists
    FOR EACH ROW
    WHEN (NEW.name IS DISTINCT FROM OLD.name)
    EXECUTE FUNCTION fn_song_history_on_artist_update();
");

            // genres: fire on UPDATE of name; affected songs = songs tagged with this genre.
            migrationBuilder.Sql(@"
CREATE OR REPLACE FUNCTION fn_song_history_on_genre_update()
RETURNS TRIGGER
LANGUAGE plpgsql
AS $$
DECLARE
    affected_id bigint;
BEGIN
    IF NEW.name IS DISTINCT FROM OLD.name
    THEN
        FOR affected_id IN
            SELECT sg.song_id FROM song_genres sg WHERE sg.genre_id = NEW.id
        LOOP
            INSERT INTO song_history_queues (song_id, song_revision, transaction_id, data, created_at)
            VALUES (
                affected_id,
                next_song_revision(affected_id),
                txid_current(),
                song_history_build_snapshot(affected_id, false, 'updated')::text,
                NOW()
            );
        END LOOP;
    END IF;
    RETURN NEW;
END;
$$;

DROP TRIGGER IF EXISTS trg_song_history_on_genre_update ON genres;
CREATE TRIGGER trg_song_history_on_genre_update
    AFTER UPDATE ON genres
    FOR EACH ROW
    WHEN (NEW.name IS DISTINCT FROM OLD.name)
    EXECUTE FUNCTION fn_song_history_on_genre_update();
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Drop the new principal-table triggers and their functions.
            migrationBuilder.Sql(@"
DROP TRIGGER IF EXISTS trg_song_history_on_album_update ON albums;
DROP TRIGGER IF EXISTS trg_song_history_on_artist_update ON artists;
DROP TRIGGER IF EXISTS trg_song_history_on_genre_update ON genres;

DROP FUNCTION IF EXISTS fn_song_history_on_album_update();
DROP FUNCTION IF EXISTS fn_song_history_on_artist_update();
DROP FUNCTION IF EXISTS fn_song_history_on_genre_update();
");

            // Restore the original song_history_build_snapshot body (album without artist).
            migrationBuilder.Sql(@"
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
        }
    }
}