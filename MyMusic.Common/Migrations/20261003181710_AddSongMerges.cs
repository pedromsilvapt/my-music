using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace MyMusic.Common.Migrations
{
    /// <inheritdoc />
    public partial class AddSongMerges : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "song_merges",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    kept_song_id = table.Column<long>(type: "bigint", nullable: false),
                    merged_song_id = table.Column<long>(type: "bigint", nullable: false),
                    owner_id = table.Column<long>(type: "bigint", nullable: false),
                    kind = table.Column<int>(type: "integer", nullable: false),
                    merged_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_song_merges", x => x.id);
                    table.ForeignKey(
                        name: "fk_song_merges_users_owner_id",
                        column: x => x.owner_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_song_merges_kept_song_id",
                table: "song_merges",
                column: "kept_song_id");

            migrationBuilder.CreateIndex(
                name: "ix_song_merges_merged_song_id",
                table: "song_merges",
                column: "merged_song_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_song_merges_owner_id",
                table: "song_merges",
                column: "owner_id");

            // Replace song_history_build_snapshot so the snapshot also lists the songs merged directly into this
            // one. Songs merged into those are not listed: the full lineage is a recursive walk of song_merges.
            migrationBuilder.Sql(BuildSnapshotFunctionSql(includeMergedSongs: true));

            // song_merges BEFORE INSERT: enqueue the kept song's snapshot, so the merge shows up in its history.
            // Rows are never updated or deleted while the kept song lives, so no other trigger is needed.
            migrationBuilder.Sql(@"
CREATE OR REPLACE FUNCTION fn_song_history_on_merge_insert()
RETURNS TRIGGER
LANGUAGE plpgsql
AS $$
BEGIN
    INSERT INTO song_history_queues (song_id, song_revision, transaction_id, data, created_at)
    VALUES (
        NEW.kept_song_id,
        next_song_revision(NEW.kept_song_id),
        txid_current(),
        song_history_build_snapshot(NEW.kept_song_id, false, 'updated')::text,
        NOW()
    );
    RETURN NEW;
END;
$$;

DROP TRIGGER IF EXISTS trg_song_history_on_merge_insert ON song_merges;
CREATE TRIGGER trg_song_history_on_merge_insert
    BEFORE INSERT ON song_merges
    FOR EACH ROW
    EXECUTE FUNCTION fn_song_history_on_merge_insert();
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
DROP TRIGGER IF EXISTS trg_song_history_on_merge_insert ON song_merges;
DROP FUNCTION IF EXISTS fn_song_history_on_merge_insert();
");

            // Restore the previous song_history_build_snapshot body (without merged_songs), before the table it
            // reads goes away.
            migrationBuilder.Sql(BuildSnapshotFunctionSql(includeMergedSongs: false));

            migrationBuilder.DropTable(
                name: "song_merges");
        }

        private static string BuildSnapshotFunctionSql(bool includeMergedSongs)
        {
            var mergedSongs = includeMergedSongs
                ? @",
        'merged_songs',
            COALESCE((SELECT jsonb_agg(jsonb_build_object('id', sm.merged_song_id, 'kind', sm.kind) ORDER BY sm.id)
                      FROM song_merges sm
                      WHERE sm.kept_song_id = s.id), '[]'::jsonb)"
                : "";

            return @"
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
                      WHERE sd.song_id = s.id), '[]'::jsonb)" + mergedSongs + @"
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
";
        }
    }
}
