using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyMusic.Common.Migrations
{
    /// <inheritdoc />
    public partial class GuardSongHistoryChildDeleteTriggersForDeletedSongs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // When a song is deleted with a raw SQL DELETE, FK cascades remove its child rows
            // (song_artists, song_genres, song_sources) *after* the songs row is gone. The child
            // delete triggers then build a NULL snapshot and violate the NOT NULL constraint on
            // song_history_queues.data. The song's own BEFORE DELETE trigger already queued the
            // 'deleted' snapshot, so the child triggers skip queueing when the song no longer
            // exists (checked before next_song_revision() so no revision number is consumed).
            migrationBuilder.Sql(@"
CREATE OR REPLACE FUNCTION fn_song_history_on_artist_delete()
RETURNS TRIGGER
LANGUAGE plpgsql
AS $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM songs WHERE id = OLD.song_id) THEN
        RETURN OLD;
    END IF;

    INSERT INTO song_history_queues (song_id, song_revision, transaction_id, data, created_at)
    VALUES (
        OLD.song_id,
        next_song_revision(OLD.song_id),
        txid_current(),
        song_history_build_snapshot(OLD.song_id, false, 'updated')::text,
        NOW()
    );
    RETURN OLD;
END;
$$;

CREATE OR REPLACE FUNCTION fn_song_history_on_genre_delete()
RETURNS TRIGGER
LANGUAGE plpgsql
AS $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM songs WHERE id = OLD.song_id) THEN
        RETURN OLD;
    END IF;

    INSERT INTO song_history_queues (song_id, song_revision, transaction_id, data, created_at)
    VALUES (
        OLD.song_id,
        next_song_revision(OLD.song_id),
        txid_current(),
        song_history_build_snapshot(OLD.song_id, false, 'updated')::text,
        NOW()
    );
    RETURN OLD;
END;
$$;

CREATE OR REPLACE FUNCTION fn_song_history_on_source_delete()
RETURNS TRIGGER
LANGUAGE plpgsql
AS $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM songs WHERE id = OLD.song_id) THEN
        RETURN OLD;
    END IF;

    INSERT INTO song_history_queues (song_id, song_revision, transaction_id, data, created_at)
    VALUES (
        OLD.song_id,
        next_song_revision(OLD.song_id),
        txid_current(),
        song_history_build_snapshot(OLD.song_id, false, 'updated')::text,
        NOW()
    );
    RETURN OLD;
END;
$$;

CREATE OR REPLACE FUNCTION fn_song_history_on_device_delete()
RETURNS TRIGGER
LANGUAGE plpgsql
AS $$
BEGIN
    IF OLD.song_id IS NULL OR NOT EXISTS (SELECT 1 FROM songs WHERE id = OLD.song_id) THEN
        RETURN OLD;
    END IF;

    INSERT INTO song_history_queues (song_id, song_revision, transaction_id, data, created_at)
    VALUES (
        OLD.song_id,
        next_song_revision(OLD.song_id),
        txid_current(),
        song_history_build_snapshot(OLD.song_id, false, 'updated')::text,
        NOW()
    );
    RETURN OLD;
END;
$$;
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Restore the unguarded trigger functions (pre-fix behavior).
            migrationBuilder.Sql(@"
CREATE OR REPLACE FUNCTION fn_song_history_on_artist_delete()
RETURNS TRIGGER
LANGUAGE plpgsql
AS $$
BEGIN
    INSERT INTO song_history_queues (song_id, song_revision, transaction_id, data, created_at)
    VALUES (
        OLD.song_id,
        next_song_revision(OLD.song_id),
        txid_current(),
        song_history_build_snapshot(OLD.song_id, false, 'updated')::text,
        NOW()
    );
    RETURN OLD;
END;
$$;

CREATE OR REPLACE FUNCTION fn_song_history_on_genre_delete()
RETURNS TRIGGER
LANGUAGE plpgsql
AS $$
BEGIN
    INSERT INTO song_history_queues (song_id, song_revision, transaction_id, data, created_at)
    VALUES (
        OLD.song_id,
        next_song_revision(OLD.song_id),
        txid_current(),
        song_history_build_snapshot(OLD.song_id, false, 'updated')::text,
        NOW()
    );
    RETURN OLD;
END;
$$;

CREATE OR REPLACE FUNCTION fn_song_history_on_source_delete()
RETURNS TRIGGER
LANGUAGE plpgsql
AS $$
BEGIN
    INSERT INTO song_history_queues (song_id, song_revision, transaction_id, data, created_at)
    VALUES (
        OLD.song_id,
        next_song_revision(OLD.song_id),
        txid_current(),
        song_history_build_snapshot(OLD.song_id, false, 'updated')::text,
        NOW()
    );
    RETURN OLD;
END;
$$;

CREATE OR REPLACE FUNCTION fn_song_history_on_device_delete()
RETURNS TRIGGER
LANGUAGE plpgsql
AS $$
BEGIN
    IF OLD.song_id IS NOT NULL THEN
        INSERT INTO song_history_queues (song_id, song_revision, transaction_id, data, created_at)
        VALUES (
            OLD.song_id,
            next_song_revision(OLD.song_id),
            txid_current(),
            song_history_build_snapshot(OLD.song_id, false, 'updated')::text,
            NOW()
        );
    END IF;
    RETURN OLD;
END;
$$;
");
        }
    }
}
