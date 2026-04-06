using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyMusic.Common.Migrations
{
    /// <inheritdoc />
    public partial class MakeAuditNonConformityFlexible : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_audit_non_conformities_songs_song_id",
                table: "audit_non_conformities");

            migrationBuilder.DropIndex(
                name: "ix_audit_non_conformities_song_id_audit_rule_id",
                table: "audit_non_conformities");

            migrationBuilder.AlterColumn<long>(
                name: "song_id",
                table: "audit_non_conformities",
                type: "bigint",
                nullable: true,
                oldClrType: typeof(long),
                oldType: "bigint");

            migrationBuilder.AddColumn<string>(
                name: "data",
                table: "audit_non_conformities",
                type: "text",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_audit_non_conformities_song_id_audit_rule_id",
                table: "audit_non_conformities",
                columns: new[] { "song_id", "audit_rule_id" });

            migrationBuilder.AddForeignKey(
                name: "fk_audit_non_conformities_songs_song_id",
                table: "audit_non_conformities",
                column: "song_id",
                principalTable: "songs",
                principalColumn: "id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_audit_non_conformities_songs_song_id",
                table: "audit_non_conformities");

            migrationBuilder.DropIndex(
                name: "ix_audit_non_conformities_song_id_audit_rule_id",
                table: "audit_non_conformities");

            migrationBuilder.DropColumn(
                name: "data",
                table: "audit_non_conformities");

            migrationBuilder.AlterColumn<long>(
                name: "song_id",
                table: "audit_non_conformities",
                type: "bigint",
                nullable: false,
                defaultValue: 0L,
                oldClrType: typeof(long),
                oldType: "bigint",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_audit_non_conformities_song_id_audit_rule_id",
                table: "audit_non_conformities",
                columns: new[] { "song_id", "audit_rule_id" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "fk_audit_non_conformities_songs_song_id",
                table: "audit_non_conformities",
                column: "song_id",
                principalTable: "songs",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
