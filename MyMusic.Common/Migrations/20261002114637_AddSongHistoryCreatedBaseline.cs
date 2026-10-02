using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyMusic.Common.Migrations
{
    /// <inheritdoc />
    public partial class AddSongHistoryCreatedBaseline : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "action",
                table: "song_histories",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "updated");

            // Existing revisions carry their action inside the diff; none of them is a baseline yet, the history
            // worker backfills those
            migrationBuilder.Sql(@"
UPDATE song_histories
SET action = diff::jsonb->>'action'
WHERE diff::jsonb->>'action' <> 'updated';
");

            migrationBuilder.CreateIndex(
                name: "ix_song_histories_song_id_created",
                table: "song_histories",
                column: "song_id",
                unique: true,
                filter: "action = 'created'");

            // songs AFTER INSERT: enqueue a 'created' entry, so every new song gets its baseline revision. The song's
            // artists, genres, etc. are inserted later in the same transaction; their queue entries share its
            // transaction id, so the worker folds them into the baseline instead of recording them as edits.
            migrationBuilder.Sql(@"
CREATE OR REPLACE FUNCTION fn_song_history_on_insert()
RETURNS TRIGGER
LANGUAGE plpgsql
AS $$
BEGIN
    INSERT INTO song_history_queues (song_id, song_revision, transaction_id, data, created_at)
    VALUES (
        NEW.id,
        next_song_revision(NEW.id),
        txid_current(),
        song_history_build_snapshot(NEW.id, false, 'created')::text,
        NOW()
    );
    RETURN NEW;
END;
$$;

DROP TRIGGER IF EXISTS trg_song_history_on_insert ON songs;
CREATE TRIGGER trg_song_history_on_insert
    AFTER INSERT ON songs
    FOR EACH ROW
    EXECUTE FUNCTION fn_song_history_on_insert();
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
DROP TRIGGER IF EXISTS trg_song_history_on_insert ON songs;
DROP FUNCTION IF EXISTS fn_song_history_on_insert();
");

            migrationBuilder.DropIndex(
                name: "ix_song_histories_song_id_created",
                table: "song_histories");

            migrationBuilder.DropColumn(
                name: "action",
                table: "song_histories");
        }
    }
}
