using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyMusic.Common.Migrations
{
    /// <inheritdoc />
    public partial class AddSongChecksumHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "song_checksums",
                columns: table => new
                {
                    song_id = table.Column<long>(type: "bigint", nullable: false),
                    checksum = table.Column<string>(type: "character varying(88)", maxLength: 88, nullable: false),
                    checksum_algorithm = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_song_checksums", x => new { x.song_id, x.checksum_algorithm, x.checksum });
                    table.ForeignKey(
                        name: "fk_song_checksums_songs_song_id",
                        column: x => x.song_id,
                        principalTable: "songs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_song_checksums_checksum_algorithm_checksum",
                table: "song_checksums",
                columns: new[] { "checksum_algorithm", "checksum" });

            // Only previous checksums are recorded here: the current one lives in songs.checksum. When a song's
            // checksum changes, the replaced one is recorded (created_at = when it was replaced). A checksum a song
            // now has is removed from the history of every song of the same owner (itself included, when its file
            // went back to an older version), so a checksum always belongs solely to the song that currently has it.
            // Row triggers fire for every write path (EF, raw SQL, bulk updates), so none can skip it.
            migrationBuilder.Sql(@"
CREATE OR REPLACE FUNCTION fn_song_checksum_history()
RETURNS TRIGGER
LANGUAGE plpgsql
AS $$
BEGIN
    IF TG_OP = 'UPDATE' THEN
        IF OLD.checksum IS NOT DISTINCT FROM NEW.checksum
           AND OLD.checksum_algorithm IS NOT DISTINCT FROM NEW.checksum_algorithm THEN
            RETURN NULL;
        END IF;

        INSERT INTO song_checksums (song_id, checksum_algorithm, checksum, created_at)
        VALUES (OLD.id, OLD.checksum_algorithm, OLD.checksum, now())
        ON CONFLICT (song_id, checksum_algorithm, checksum) DO UPDATE SET created_at = EXCLUDED.created_at;
    END IF;

    DELETE FROM song_checksums sc
    USING songs s
    WHERE sc.song_id = s.id
      AND s.owner_id = NEW.owner_id
      AND sc.checksum_algorithm = NEW.checksum_algorithm
      AND sc.checksum = NEW.checksum;

    RETURN NULL;
END;
$$;

CREATE TRIGGER trg_song_checksum_history
AFTER INSERT OR UPDATE OF checksum, checksum_algorithm ON songs
FOR EACH ROW
EXECUTE FUNCTION fn_song_checksum_history();
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
DROP TRIGGER IF EXISTS trg_song_checksum_history ON songs;
DROP FUNCTION IF EXISTS fn_song_checksum_history();
");

            migrationBuilder.DropTable(
                name: "song_checksums");
        }
    }
}
