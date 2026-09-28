using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyMusic.Common.Migrations
{
    /// <inheritdoc />
    public partial class AddSongRepositoryPathUniqueIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_songs_owner_id",
                table: "songs");

            migrationBuilder.CreateIndex(
                name: "ix_songs_owner_id_repository_path",
                table: "songs",
                columns: new[] { "owner_id", "repository_path" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_songs_owner_id_repository_path",
                table: "songs");

            migrationBuilder.CreateIndex(
                name: "ix_songs_owner_id",
                table: "songs",
                column: "owner_id");
        }
    }
}
