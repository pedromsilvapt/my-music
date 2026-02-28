using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace MyMusic.Common.Migrations
{
    /// <inheritdoc />
    public partial class AddPlayHistoryAndPlayCount : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "play_count",
                table: "songs",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "play_histories",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    song_id = table.Column<long>(type: "bigint", nullable: false),
                    owner_id = table.Column<long>(type: "bigint", nullable: false),
                    device_id = table.Column<long>(type: "bigint", nullable: true),
                    client_id = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: false),
                    played_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_play_histories", x => x.id);
                    table.ForeignKey(
                        name: "fk_play_histories_devices_device_id",
                        column: x => x.device_id,
                        principalTable: "devices",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "fk_play_histories_songs_song_id",
                        column: x => x.song_id,
                        principalTable: "songs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_play_histories_users_owner_id",
                        column: x => x.owner_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_play_histories_device_id",
                table: "play_histories",
                column: "device_id");

            migrationBuilder.CreateIndex(
                name: "ix_play_histories_owner_id_client_id",
                table: "play_histories",
                columns: new[] { "owner_id", "client_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_play_histories_song_id",
                table: "play_histories",
                column: "song_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "play_histories");

            migrationBuilder.DropColumn(
                name: "play_count",
                table: "songs");
        }
    }
}
