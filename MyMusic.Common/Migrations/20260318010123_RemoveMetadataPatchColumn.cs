using System.Text.Json;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyMusic.Common.Migrations
{
    /// <inheritdoc />
    public partial class RemoveMetadataPatchColumn : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "metadata_patch",
                table: "auto_fetched_metadata");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<JsonElement>(
                name: "metadata_patch",
                table: "auto_fetched_metadata",
                type: "jsonb",
                nullable: false,
                defaultValue: System.Text.Json.JsonDocument.Parse("", new System.Text.Json.JsonDocumentOptions()).RootElement);
        }
    }
}
