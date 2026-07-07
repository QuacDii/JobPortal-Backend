using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TKVL.Migrations
{
    /// <inheritdoc />
    public partial class AddTableChiTietPhanTichAi : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ChiTietPhanTichAi",
                columns: table => new
                {
                    maPhanTich = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    maDon = table.Column<int>(type: "int", nullable: false),
                    diemMatchingTong = table.Column<int>(type: "int", nullable: false),
                    diemKyNang = table.Column<int>(type: "int", nullable: false),
                    diemKinhNghiem = table.Column<int>(type: "int", nullable: false),
                    diemLinhVuc = table.Column<int>(type: "int", nullable: false),
                    diemCapBac = table.Column<int>(type: "int", nullable: false),
                    diemManhTieuBieu = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    diemConThieu = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    thongTinHoSoTrichXuatJson = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChiTietPhanTichAi", x => x.maPhanTich);
                    table.ForeignKey(
                        name: "FK_ChiTietPhanTichAi_DonUngTuyen",
                        column: x => x.maDon,
                        principalTable: "DonUngTuyen",
                        principalColumn: "maDon",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ChiTietPhanTichAi_maDon",
                table: "ChiTietPhanTichAi",
                column: "maDon",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ChiTietPhanTichAi");
        }
    }
}
