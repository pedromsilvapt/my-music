using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace MyMusic.Common.Migrations
{
    /// <inheritdoc />
    public partial class AddAcousticFingerprintAndExcludedDuplicatePair : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "excluded_duplicate_pairs",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    song_a_id = table.Column<long>(type: "bigint", nullable: false),
                    song_b_id = table.Column<long>(type: "bigint", nullable: false),
                    owner_id = table.Column<long>(type: "bigint", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    reason = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_excluded_duplicate_pairs", x => x.id);
                    table.ForeignKey(
                        name: "fk_excluded_duplicate_pairs_songs_song_a_id",
                        column: x => x.song_a_id,
                        principalTable: "songs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_excluded_duplicate_pairs_songs_song_b_id",
                        column: x => x.song_b_id,
                        principalTable: "songs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_excluded_duplicate_pairs_users_owner_id",
                        column: x => x.owner_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "song_acoustic_fingerprints",
                columns: table => new
                {
                    checksum = table.Column<string>(type: "text", nullable: false),
                    checksum_algorithm = table.Column<string>(type: "text", nullable: false),
                    owner_id = table.Column<long>(type: "bigint", nullable: false),
                    fingerprint = table.Column<byte[]>(type: "bytea", nullable: false),
                    duration = table.Column<double>(type: "double precision", nullable: false),
                    fingerprint_length = table.Column<double>(type: "double precision", nullable: false),
                    fingerprint_algorithm = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    modified_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_song_acoustic_fingerprints", x => new { x.checksum, x.checksum_algorithm, x.owner_id });
                    table.ForeignKey(
                        name: "fk_song_acoustic_fingerprints_users_owner_id",
                        column: x => x.owner_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_excluded_duplicate_pairs_owner_id",
                table: "excluded_duplicate_pairs",
                column: "owner_id");

            migrationBuilder.CreateIndex(
                name: "ix_excluded_duplicate_pairs_song_a_id_song_b_id_owner_id",
                table: "excluded_duplicate_pairs",
                columns: new[] { "song_a_id", "song_b_id", "owner_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_excluded_duplicate_pairs_song_b_id",
                table: "excluded_duplicate_pairs",
                column: "song_b_id");

            migrationBuilder.CreateIndex(
                name: "ix_song_acoustic_fingerprints_owner_id",
                table: "song_acoustic_fingerprints",
                column: "owner_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "excluded_duplicate_pairs");

            migrationBuilder.DropTable(
                name: "song_acoustic_fingerprints");
        }
    }
}
