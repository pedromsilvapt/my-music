using System;
using System.Text.Json;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace MyMusic.Common.Migrations
{
    /// <inheritdoc />
    public partial class AddAutoFetchedMetadata : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "auto_fetched_metadata",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    song_id = table.Column<long>(type: "bigint", nullable: false),
                    metadata_patch = table.Column<JsonElement>(type: "jsonb", nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    source_id = table.Column<long>(type: "bigint", nullable: true),
                    fetched_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    error_message = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_auto_fetched_metadata", x => x.id);
                    table.ForeignKey(
                        name: "fk_auto_fetched_metadata_songs_song_id",
                        column: x => x.song_id,
                        principalTable: "songs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_auto_fetched_metadata_sources_source_id",
                        column: x => x.source_id,
                        principalTable: "sources",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "metadata_fetch_tasks",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    song_id = table.Column<long>(type: "bigint", nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    progress = table.Column<int>(type: "integer", nullable: false),
                    error_message = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    started_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    completed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_metadata_fetch_tasks", x => x.id);
                    table.ForeignKey(
                        name: "fk_metadata_fetch_tasks_songs_song_id",
                        column: x => x.song_id,
                        principalTable: "songs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_auto_fetched_metadata_song_id_fetched_at",
                table: "auto_fetched_metadata",
                columns: new[] { "song_id", "fetched_at" });

            migrationBuilder.CreateIndex(
                name: "ix_auto_fetched_metadata_song_id_status",
                table: "auto_fetched_metadata",
                columns: new[] { "song_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_auto_fetched_metadata_source_id",
                table: "auto_fetched_metadata",
                column: "source_id");

            migrationBuilder.CreateIndex(
                name: "ix_auto_fetched_metadata_status_fetched_at",
                table: "auto_fetched_metadata",
                columns: new[] { "status", "fetched_at" });

            migrationBuilder.CreateIndex(
                name: "ix_metadata_fetch_tasks_song_id_status",
                table: "metadata_fetch_tasks",
                columns: new[] { "song_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_metadata_fetch_tasks_status_created_at",
                table: "metadata_fetch_tasks",
                columns: new[] { "status", "created_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "auto_fetched_metadata");

            migrationBuilder.DropTable(
                name: "metadata_fetch_tasks");
        }
    }
}
