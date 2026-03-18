using System.Text.Json;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyMusic.Common.Migrations
{
    /// <inheritdoc />
    public partial class AddSourceMetadataToAutoFetchedMetadata : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<JsonElement>(
                name: "source_metadata",
                table: "auto_fetched_metadata",
                type: "jsonb",
                nullable: false,
                defaultValue: System.Text.Json.JsonDocument.Parse("{}", new System.Text.Json.JsonDocumentOptions()).RootElement);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "source_metadata",
                table: "auto_fetched_metadata");
        }
    }
}
