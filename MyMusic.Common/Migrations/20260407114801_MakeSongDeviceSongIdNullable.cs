using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyMusic.Common.Migrations
{
    /// <inheritdoc />
    public partial class MakeSongDeviceSongIdNullable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_song_devices_songs_song_id",
                table: "song_devices");

            migrationBuilder.AlterColumn<long>(
                name: "song_id",
                table: "song_devices",
                type: "bigint",
                nullable: true,
                oldClrType: typeof(long),
                oldType: "bigint");

            migrationBuilder.AddForeignKey(
                name: "fk_song_devices_songs_song_id",
                table: "song_devices",
                column: "song_id",
                principalTable: "songs",
                principalColumn: "id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_song_devices_songs_song_id",
                table: "song_devices");

            migrationBuilder.AlterColumn<long>(
                name: "song_id",
                table: "song_devices",
                type: "bigint",
                nullable: false,
                defaultValue: 0L,
                oldClrType: typeof(long),
                oldType: "bigint",
                oldNullable: true);

            migrationBuilder.AddForeignKey(
                name: "fk_song_devices_songs_song_id",
                table: "song_devices",
                column: "song_id",
                principalTable: "songs",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
