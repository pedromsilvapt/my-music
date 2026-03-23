using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyMusic.Common.Migrations
{
    /// <inheritdoc />
    public partial class ChangePlaylistSongOrderToDouble : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Convert existing integer orders to double precision with gap spacing
            // Multiply by 1000 to create gaps for efficient insertions
            migrationBuilder.Sql(@"
                UPDATE playlist_songs
                SET ""order"" = ""order"" * 1000.0
            ");

            migrationBuilder.DropIndex(
                name: "ix_playlist_songs_playlist_id",
                table: "playlist_songs");

            migrationBuilder.AlterColumn<double>(
                name: "order",
                table: "playlist_songs",
                type: "double precision",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.CreateIndex(
                name: "ix_playlist_songs_playlist_id_order",
                table: "playlist_songs",
                columns: new[] { "playlist_id", "order" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_playlist_songs_playlist_id_order",
                table: "playlist_songs");

            migrationBuilder.AlterColumn<int>(
                name: "order",
                table: "playlist_songs",
                type: "integer",
                nullable: false,
                oldClrType: typeof(double),
                oldType: "double precision");

            // Convert back to integer orders
            migrationBuilder.Sql(@"
                UPDATE playlist_songs
                SET ""order"" = CAST(""order"" / 1000.0 AS integer)
            ");

            migrationBuilder.CreateIndex(
                name: "ix_playlist_songs_playlist_id",
                table: "playlist_songs",
                column: "playlist_id");
        }
    }
}
