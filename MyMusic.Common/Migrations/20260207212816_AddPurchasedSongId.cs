using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyMusic.Common.Migrations
{
    /// <inheritdoc />
    public partial class AddPurchasedSongId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "cover",
                table: "purchased_songs",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "song_id",
                table: "purchased_songs",
                type: "bigint",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_purchased_songs_song_id",
                table: "purchased_songs",
                column: "song_id");

            migrationBuilder.AddForeignKey(
                name: "fk_purchased_songs_songs_song_id",
                table: "purchased_songs",
                column: "song_id",
                principalTable: "songs",
                principalColumn: "id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_purchased_songs_songs_song_id",
                table: "purchased_songs");

            migrationBuilder.DropIndex(
                name: "ix_purchased_songs_song_id",
                table: "purchased_songs");

            migrationBuilder.DropColumn(
                name: "cover",
                table: "purchased_songs");

            migrationBuilder.DropColumn(
                name: "song_id",
                table: "purchased_songs");
        }
    }
}
