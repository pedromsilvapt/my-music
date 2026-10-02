using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyMusic.Common.Migrations
{
    /// <inheritdoc />
    public partial class ExcludePlayCountFromSongHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // play_count changes on every playback; it is not a song edit, so
            // an update touching only play_count should not queue a history entry.
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
        OR NEW.repository_path IS DISTINCT FROM OLD.repository_path
        OR NEW.checksum IS DISTINCT FROM OLD.checksum
        OR NEW.checksum_algorithm IS DISTINCT FROM OLD.checksum_algorithm
        OR NEW.added_at IS DISTINCT FROM OLD.added_at
        OR NEW.created_at IS DISTINCT FROM OLD.created_at
    THEN
        INSERT INTO song_history_queues (song_id, song_revision, transaction_id, data, created_at)
        VALUES (
            NEW.id,
            next_song_revision(NEW.id),
            txid_current(),
            song_history_build_snapshot(NEW.id, (OLD.cover_id IS DISTINCT FROM NEW.cover_id), 'updated')::text,
            NOW()
        );
    END IF;
    RETURN NEW;
END;
$$;
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
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
        INSERT INTO song_history_queues (song_id, song_revision, transaction_id, data, created_at)
        VALUES (
            NEW.id,
            next_song_revision(NEW.id),
            txid_current(),
            song_history_build_snapshot(NEW.id, (OLD.cover_id IS DISTINCT FROM NEW.cover_id), 'updated')::text,
            NOW()
        );
    END IF;
    RETURN NEW;
END;
$$;
");
        }
    }
}
