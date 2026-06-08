using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TKVL.Migrations
{
    /// <inheritdoc />
    public partial class RenameSoNgayToDonViThoiGian : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "soNgayHieuLuc",
                table: "GoiDichVu");

            migrationBuilder.AddColumn<int>(
                name: "donViThoiGian",
                table: "GoiDichVu",
                type: "int",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "donViThoiGian",
                table: "GoiDichVu");

            migrationBuilder.AddColumn<int>(
                name: "soNgayHieuLuc",
                table: "GoiDichVu",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }
    }
}
