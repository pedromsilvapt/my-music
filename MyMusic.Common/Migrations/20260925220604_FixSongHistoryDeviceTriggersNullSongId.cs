using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyMusic.Common.Migrations
{
    /// <inheritdoc />
    public partial class FixSongHistoryDeviceTriggersNullSongId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // song_devices.song_id is nullable (tombstone rows have NULL SongId). The device
            // history triggers must skip queueing a snapshot instead of violating the NOT NULL
            // constraint on song_history_queues.song_id. Mirrors the existing guard in
            // fn_song_history_on_device_update().
            migrationBuilder.Sql(@"
CREATE OR REPLACE FUNCTION fn_song_history_on_device_insert()
RETURNS TRIGGER
LANGUAGE plpgsql
AS $$
BEGIN
    IF NEW.song_id IS NOT NULL THEN
        INSERT INTO song_history_queues (song_id, song_revision, transaction_id, data, created_at)
        VALUES (
            NEW.song_id,
            next_song_revision(NEW.song_id),
            txid_current(),
            song_history_build_snapshot(NEW.song_id, false, 'updated')::text,
            NOW()
        );
    END IF;
    RETURN NEW;
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

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Restore the unguarded trigger functions (pre-fix behavior).
            migrationBuilder.Sql(@"
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
");
        }
    }
}