using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace MyMusic.Common.Migrations
{
    /// <inheritdoc />
    public partial class ReplaceSongSharingWithPlaylistSharing : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "song_sharings");

            migrationBuilder.CreateTable(
                name: "playlist_sharings",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    playlist_id = table.Column<long>(type: "bigint", nullable: false),
                    user_id = table.Column<long>(type: "bigint", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    modified_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_playlist_sharings", x => x.id);
                    table.ForeignKey(
                        name: "fk_playlist_sharings_playlists_playlist_id",
                        column: x => x.playlist_id,
                        principalTable: "playlists",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_playlist_sharings_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_playlist_sharings_playlist_id_user_id",
                table: "playlist_sharings",
                columns: new[] { "playlist_id", "user_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_playlist_sharings_user_id",
                table: "playlist_sharings",
                column: "user_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "playlist_sharings");

            migrationBuilder.CreateTable(
                name: "song_sharings",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    song_id = table.Column<long>(type: "bigint", nullable: false),
                    user_id = table.Column<long>(type: "bigint", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    modified_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_song_sharings", x => x.id);
                    table.ForeignKey(
                        name: "fk_song_sharings_songs_song_id",
                        column: x => x.song_id,
                        principalTable: "songs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_song_sharings_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_song_sharings_song_id_user_id",
                table: "song_sharings",
                columns: new[] { "song_id", "user_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_song_sharings_user_id",
                table: "song_sharings",
                column: "user_id");
        }
    }
}
