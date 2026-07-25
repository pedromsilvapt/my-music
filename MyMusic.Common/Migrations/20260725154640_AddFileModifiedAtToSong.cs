using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyMusic.Common.Migrations
{
    /// <inheritdoc />
    public partial class AddFileModifiedAtToSong : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "file_modified_at",
                table: "songs",
                type: "timestamp with time zone",
                nullable: true);

            // Backfill: initially identical to existing ModifiedAt for all existing rows
            migrationBuilder.Sql("UPDATE songs SET file_modified_at = modified_at");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "file_modified_at",
                table: "songs");
        }
    }
}
