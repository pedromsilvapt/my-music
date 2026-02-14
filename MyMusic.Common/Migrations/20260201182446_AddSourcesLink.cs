using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyMusic.Common.Migrations
{
    /// <inheritdoc />
    public partial class AddSourcesLink : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "link",
                table: "song_sources",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "link",
                table: "artist_sources",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "link",
                table: "album_sources",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "link",
                table: "song_sources");

            migrationBuilder.DropColumn(
                name: "link",
                table: "artist_sources");

            migrationBuilder.DropColumn(
                name: "link",
                table: "album_sources");
        }
    }
}
