using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace MyMusic.Common.Migrations
{
    /// <inheritdoc />
    public partial class AddSyncSession : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "device_sync_sessions",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    device_id = table.Column<long>(type: "bigint", nullable: false),
                    started_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    completed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    status = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_device_sync_sessions", x => x.id);
                    table.ForeignKey(
                        name: "fk_device_sync_sessions_devices_device_id",
                        column: x => x.device_id,
                        principalTable: "devices",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "device_sync_session_records",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    session_id = table.Column<long>(type: "bigint", nullable: false),
                    file_path = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: false),
                    song_id = table.Column<long>(type: "bigint", nullable: true),
                    action = table.Column<int>(type: "integer", nullable: false),
                    processed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_device_sync_session_records", x => x.id);
                    table.ForeignKey(
                        name: "fk_device_sync_session_records_device_sync_sessions_session_id",
                        column: x => x.session_id,
                        principalTable: "device_sync_sessions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_device_sync_session_records_session_id_file_path",
                table: "device_sync_session_records",
                columns: new[] { "session_id", "file_path" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_device_sync_sessions_device_id",
                table: "device_sync_sessions",
                column: "device_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "device_sync_session_records");

            migrationBuilder.DropTable(
                name: "device_sync_sessions");
        }
    }
}
