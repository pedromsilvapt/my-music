using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyMusic.Common.Migrations
{
    /// <inheritdoc />
    public partial class AddSongHistoryQueueTransactionId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "transaction_id",
                table: "song_history_queues",
                type: "bigint",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_song_history_queues_song_id_transaction_id",
                table: "song_history_queues",
                columns: new[] { "song_id", "transaction_id" });

            // Recreate all trigger functions to include txid_current() as the
            // transaction_id value, enabling the worker to compact queue entries
            // produced within the same DB transaction into a single history row.
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

    INSERT INTO song_history_queues (song_id, song_revision, transaction_id, data, created_at)
    VALUES (
        OLD.id,
        next_song_revision(OLD.id),
        txid_current(),
        song_history_build_snapshot(OLD.id, (OLD.cover_id IS NOT NULL), 'deleted')::text,
        NOW()
    );
    RETURN OLD;
END;
$$;

CREATE OR REPLACE FUNCTION fn_song_history_on_artist_insert()
RETURNS TRIGGER
LANGUAGE plpgsql
AS $$
BEGIN
    INSERT INTO song_history_queues (song_id, song_revision, transaction_id, data, created_at)
    VALUES (
        NEW.song_id,
        next_song_revision(NEW.song_id),
        txid_current(),
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

CREATE OR REPLACE FUNCTION fn_song_history_on_genre_insert()
RETURNS TRIGGER
LANGUAGE plpgsql
AS $$
BEGIN
    INSERT INTO song_history_queues (song_id, song_revision, transaction_id, data, created_at)
    VALUES (
        NEW.song_id,
        next_song_revision(NEW.song_id),
        txid_current(),
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

CREATE OR REPLACE FUNCTION fn_song_history_on_source_insert()
RETURNS TRIGGER
LANGUAGE plpgsql
AS $$
BEGIN
    INSERT INTO song_history_queues (song_id, song_revision, transaction_id, data, created_at)
    VALUES (
        NEW.song_id,
        next_song_revision(NEW.song_id),
        txid_current(),
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

CREATE OR REPLACE FUNCTION fn_song_history_on_device_insert()
RETURNS TRIGGER
LANGUAGE plpgsql
AS $$
BEGIN
    INSERT INTO song_history_queues (song_id, song_revision, transaction_id, data, created_at)
    VALUES (
        NEW.song_id,
        next_song_revision(NEW.song_id),
        txid_current(),
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

CREATE OR REPLACE FUNCTION fn_song_history_on_device_update()
RETURNS TRIGGER
LANGUAGE plpgsql
AS $$
BEGIN
    IF OLD.song_id IS NOT NULL AND NEW.song_id IS NULL THEN
        INSERT INTO song_history_queues (song_id, song_revision, transaction_id, data, created_at)
        VALUES (
            OLD.song_id,
            next_song_revision(OLD.song_id),
            txid_current(),
            song_history_build_snapshot(OLD.song_id, false, 'updated')::text,
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
            // Restore the original trigger functions without transaction_id.
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
");

            migrationBuilder.DropIndex(
                name: "ix_song_history_queues_song_id_transaction_id",
                table: "song_history_queues");

            migrationBuilder.DropColumn(
                name: "transaction_id",
                table: "song_history_queues");
        }
    }
}
