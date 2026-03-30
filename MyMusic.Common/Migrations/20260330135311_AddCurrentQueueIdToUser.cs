using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyMusic.Common.Migrations
{
    /// <inheritdoc />
    public partial class AddCurrentQueueIdToUser : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "current_queue_id",
                table: "users",
                type: "bigint",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_users_current_queue_id",
                table: "users",
                column: "current_queue_id",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "fk_users_playlists_current_queue_id",
                table: "users",
                column: "current_queue_id",
                principalTable: "playlists",
                principalColumn: "id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_users_playlists_current_queue_id",
                table: "users");

            migrationBuilder.DropIndex(
                name: "ix_users_current_queue_id",
                table: "users");

            migrationBuilder.DropColumn(
                name: "current_queue_id",
                table: "users");
        }
    }
}
