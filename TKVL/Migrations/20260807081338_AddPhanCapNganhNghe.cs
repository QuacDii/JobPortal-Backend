using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TKVL.Migrations
{
    /// <inheritdoc />
    public partial class AddPhanCapNganhNghe : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "MaNganhCha",
                table: "NganhNghe",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_NganhNghe_MaNganhCha",
                table: "NganhNghe",
                column: "MaNganhCha");

            migrationBuilder.AddForeignKey(
                name: "FK_NganhNghe_NganhNghe_MaNganhCha",
                table: "NganhNghe",
                column: "MaNganhCha",
                principalTable: "NganhNghe",
                principalColumn: "maNganh",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_NganhNghe_NganhNghe_MaNganhCha",
                table: "NganhNghe");

            migrationBuilder.DropIndex(
                name: "IX_NganhNghe_MaNganhCha",
                table: "NganhNghe");

            migrationBuilder.DropColumn(
                name: "MaNganhCha",
                table: "NganhNghe");
        }
    }
}
