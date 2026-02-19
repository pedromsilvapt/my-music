using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyMusic.Common.Migrations
{
    /// <inheritdoc />
    public partial class AddSyncRecordSourceAndActions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "source",
                table: "device_sync_session_records",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "source",
                table: "device_sync_session_records");
        }
    }
}
