using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TKVL.Migrations
{
    /// <inheritdoc />
    public partial class RenameGiaKhuyenMaiToCamelCase : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "GiaKhuyenMai",
                table: "GoiDichVu",
                newName: "giaKhuyenMai");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "giaKhuyenMai",
                table: "GoiDichVu",
                newName: "GiaKhuyenMai");
        }
    }
}
