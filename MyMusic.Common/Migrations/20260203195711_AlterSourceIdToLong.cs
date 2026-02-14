using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace MyMusic.Common.Migrations
{
    /// <inheritdoc />
    public partial class AlterSourceIdToLong : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_album_sources_sources_source_id1",
                table: "album_sources");

            migrationBuilder.DropForeignKey(
                name: "fk_artist_sources_sources_source_id1",
                table: "artist_sources");

            migrationBuilder.DropForeignKey(
                name: "fk_song_sources_sources_source_id1",
                table: "song_sources");

            migrationBuilder.DropIndex(
                name: "ix_song_sources_source_id1",
                table: "song_sources");

            migrationBuilder.DropIndex(
                name: "ix_artist_sources_source_id1",
                table: "artist_sources");

            migrationBuilder.DropIndex(
                name: "ix_album_sources_source_id1",
                table: "album_sources");

            migrationBuilder.DropColumn(
                name: "source_id1",
                table: "song_sources");

            migrationBuilder.DropColumn(
                name: "source_id1",
                table: "artist_sources");

            migrationBuilder.DropColumn(
                name: "source_id1",
                table: "album_sources");

            migrationBuilder.AlterColumn<long>(
                name: "id",
                table: "sources",
                type: "bigint",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer")
                .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn)
                .OldAnnotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn);

            migrationBuilder.AlterColumn<long>(
                name: "id",
                table: "song_sources",
                type: "bigint",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer")
                .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn)
                .OldAnnotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn);

            migrationBuilder.CreateIndex(
                name: "ix_song_sources_source_id",
                table: "song_sources",
                column: "source_id");

            migrationBuilder.CreateIndex(
                name: "ix_artist_sources_source_id",
                table: "artist_sources",
                column: "source_id");

            migrationBuilder.CreateIndex(
                name: "ix_album_sources_source_id",
                table: "album_sources",
                column: "source_id");

            migrationBuilder.AddForeignKey(
                name: "fk_album_sources_sources_source_id",
                table: "album_sources",
                column: "source_id",
                principalTable: "sources",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_artist_sources_sources_source_id",
                table: "artist_sources",
                column: "source_id",
                principalTable: "sources",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_song_sources_sources_source_id",
                table: "song_sources",
                column: "source_id",
                principalTable: "sources",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_album_sources_sources_source_id",
                table: "album_sources");

            migrationBuilder.DropForeignKey(
                name: "fk_artist_sources_sources_source_id",
                table: "artist_sources");

            migrationBuilder.DropForeignKey(
                name: "fk_song_sources_sources_source_id",
                table: "song_sources");

            migrationBuilder.DropIndex(
                name: "ix_song_sources_source_id",
                table: "song_sources");

            migrationBuilder.DropIndex(
                name: "ix_artist_sources_source_id",
                table: "artist_sources");

            migrationBuilder.DropIndex(
                name: "ix_album_sources_source_id",
                table: "album_sources");

            migrationBuilder.AlterColumn<int>(
                name: "id",
                table: "sources",
                type: "integer",
                nullable: false,
                oldClrType: typeof(long),
                oldType: "bigint")
                .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn)
                .OldAnnotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn);

            migrationBuilder.AlterColumn<int>(
                name: "id",
                table: "song_sources",
                type: "integer",
                nullable: false,
                oldClrType: typeof(long),
                oldType: "bigint")
                .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn)
                .OldAnnotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn);

            migrationBuilder.AddColumn<int>(
                name: "source_id1",
                table: "song_sources",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "source_id1",
                table: "artist_sources",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "source_id1",
                table: "album_sources",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "ix_song_sources_source_id1",
                table: "song_sources",
                column: "source_id1");

            migrationBuilder.CreateIndex(
                name: "ix_artist_sources_source_id1",
                table: "artist_sources",
                column: "source_id1");

            migrationBuilder.CreateIndex(
                name: "ix_album_sources_source_id1",
                table: "album_sources",
                column: "source_id1");

            migrationBuilder.AddForeignKey(
                name: "fk_album_sources_sources_source_id1",
                table: "album_sources",
                column: "source_id1",
                principalTable: "sources",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_artist_sources_sources_source_id1",
                table: "artist_sources",
                column: "source_id1",
                principalTable: "sources",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_song_sources_sources_source_id1",
                table: "song_sources",
                column: "source_id1",
                principalTable: "sources",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
