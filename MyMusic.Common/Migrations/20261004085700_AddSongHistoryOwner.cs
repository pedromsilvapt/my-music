using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyMusic.Common.Migrations
{
    /// <inheritdoc />
    public partial class AddSongHistoryOwner : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Nullable until the existing rows get their owner, below
            migrationBuilder.AddColumn<long>(
                name: "owner_id",
                table: "song_history_queues",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "owner_id",
                table: "song_histories",
                type: "bigint",
                nullable: true);

            // Queue entries hold the song's snapshot, which always carries its owner
            migrationBuilder.Sql(@"
UPDATE song_history_queues
SET owner_id = (data::jsonb->>'owner_id')::bigint;
");

            // History rows only hold what changed, so the owner comes from, in order: the song itself, if it still
            // exists; its 'created' baseline; the record of it being merged away, or of it absorbing another song
            // (every merged song has its own song_merges row, so chains of merges need no walking); its queue entries.
            migrationBuilder.Sql(@"
UPDATE song_histories h
SET owner_id = o.owner_id
FROM (
    SELECT ids.song_id,
           COALESCE(
               (SELECT s.owner_id FROM songs s WHERE s.id = ids.song_id),
               (SELECT (c.diff::jsonb->'owner_id'->>'new')::bigint
                FROM song_histories c
                WHERE c.song_id = ids.song_id AND c.action = 'created'),
               (SELECT m.owner_id FROM song_merges m WHERE m.merged_song_id = ids.song_id),
               (SELECT m.owner_id FROM song_merges m WHERE m.kept_song_id = ids.song_id LIMIT 1),
               (SELECT q.owner_id FROM song_history_queues q
                WHERE q.song_id = ids.song_id AND q.owner_id IS NOT NULL LIMIT 1)
           ) AS owner_id
    FROM (SELECT DISTINCT song_id FROM song_histories) ids
) o
WHERE h.song_id = o.song_id;
");

            // Rows whose owner is unknown, or no longer exists, were left behind by deleted users
            migrationBuilder.Sql(@"
DELETE FROM song_history_queues q
WHERE q.owner_id IS NULL OR NOT EXISTS (SELECT 1 FROM users u WHERE u.id = q.owner_id);

DELETE FROM song_histories h
WHERE h.owner_id IS NULL OR NOT EXISTS (SELECT 1 FROM users u WHERE u.id = h.owner_id);
");

            migrationBuilder.AlterColumn<long>(
                name: "owner_id",
                table: "song_history_queues",
                type: "bigint",
                nullable: false,
                oldClrType: typeof(long),
                oldType: "bigint",
                oldNullable: true);

            migrationBuilder.AlterColumn<long>(
                name: "owner_id",
                table: "song_histories",
                type: "bigint",
                nullable: false,
                oldClrType: typeof(long),
                oldType: "bigint",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_song_history_queues_owner_id",
                table: "song_history_queues",
                column: "owner_id");

            migrationBuilder.CreateIndex(
                name: "ix_song_histories_owner_id",
                table: "song_histories",
                column: "owner_id");

            migrationBuilder.AddForeignKey(
                name: "fk_song_histories_users_owner_id",
                table: "song_histories",
                column: "owner_id",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_song_history_queues_users_owner_id",
                table: "song_history_queues",
                column: "owner_id",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            // song_history_queues BEFORE INSERT: take the owner from the snapshot, so none of the triggers queueing
            // entries has to set it.
            migrationBuilder.Sql(@"
CREATE OR REPLACE FUNCTION fn_song_history_queue_set_owner()
RETURNS TRIGGER
LANGUAGE plpgsql
AS $$
BEGIN
    IF NEW.owner_id IS NULL THEN
        NEW.owner_id := (NEW.data::jsonb->>'owner_id')::bigint;
    END IF;
    RETURN NEW;
END;
$$;

DROP TRIGGER IF EXISTS trg_song_history_queue_set_owner ON song_history_queues;
CREATE TRIGGER trg_song_history_queue_set_owner
    BEFORE INSERT ON song_history_queues
    FOR EACH ROW
    EXECUTE FUNCTION fn_song_history_queue_set_owner();
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
DROP TRIGGER IF EXISTS trg_song_history_queue_set_owner ON song_history_queues;
DROP FUNCTION IF EXISTS fn_song_history_queue_set_owner();
");

            migrationBuilder.DropForeignKey(
                name: "fk_song_histories_users_owner_id",
                table: "song_histories");

            migrationBuilder.DropForeignKey(
                name: "fk_song_history_queues_users_owner_id",
                table: "song_history_queues");

            migrationBuilder.DropIndex(
                name: "ix_song_history_queues_owner_id",
                table: "song_history_queues");

            migrationBuilder.DropIndex(
                name: "ix_song_histories_owner_id",
                table: "song_histories");

            migrationBuilder.DropColumn(
                name: "owner_id",
                table: "song_history_queues");

            migrationBuilder.DropColumn(
                name: "owner_id",
                table: "song_histories");
        }
    }
}
