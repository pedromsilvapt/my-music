using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyMusic.Common.Migrations
{
    /// <inheritdoc />
    public partial class AddWishlistUniqueConstraint : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_wishlist_items_owner_id",
                table: "wishlist_items");

            migrationBuilder.CreateIndex(
                name: "ix_wishlist_items_owner_id_source_id_query",
                table: "wishlist_items",
                columns: new[] { "owner_id", "source_id", "query" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_wishlist_items_owner_id_source_id_query",
                table: "wishlist_items");

            migrationBuilder.CreateIndex(
                name: "ix_wishlist_items_owner_id",
                table: "wishlist_items",
                column: "owner_id");
        }
    }
}
