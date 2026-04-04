using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyMusic.Common.Migrations
{
    /// <inheritdoc />
    public partial class AddWishlistFilter : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_wishlist_items_owner_id_source_id_query",
                table: "wishlist_items");

            migrationBuilder.AddColumn<string>(
                name: "filter",
                table: "wishlist_items",
                type: "character varying(1024)",
                maxLength: 1024,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_wishlist_items_owner_id_source_id_query_filter",
                table: "wishlist_items",
                columns: new[] { "owner_id", "source_id", "query", "filter" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_wishlist_items_owner_id_source_id_query_filter",
                table: "wishlist_items");

            migrationBuilder.DropColumn(
                name: "filter",
                table: "wishlist_items");

            migrationBuilder.CreateIndex(
                name: "ix_wishlist_items_owner_id_source_id_query",
                table: "wishlist_items",
                columns: new[] { "owner_id", "source_id", "query" },
                unique: true);
        }
    }
}
