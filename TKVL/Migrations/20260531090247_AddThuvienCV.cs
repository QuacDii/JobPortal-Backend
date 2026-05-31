using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TKVL.Migrations
{
    /// <inheritdoc />
    public partial class AddThuvienCV : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "MaHex",
                table: "CV",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MaMau",
                table: "CV",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "DanhMucMaus",
                columns: table => new
                {
                    MaDanhMuc = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TenDanhMuc = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DanhMucMaus", x => x.MaDanhMuc);
                });

            migrationBuilder.CreateTable(
                name: "MauCVs",
                columns: table => new
                {
                    MaMau = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TenMau = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    MoTa = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    AnhThumbnail = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    IsATS = table.Column<bool>(type: "bit", nullable: false),
                    TrangThai = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MauCVs", x => x.MaMau);
                });

            migrationBuilder.CreateTable(
                name: "MauSacs",
                columns: table => new
                {
                    MaMauSac = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MaMau = table.Column<int>(type: "int", nullable: false),
                    MaHex = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MauSacs", x => x.MaMauSac);
                    table.ForeignKey(
                        name: "FK_MauSac_MauCV",
                        column: x => x.MaMau,
                        principalTable: "MauCVs",
                        principalColumn: "MaMau",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PhanLoaiMaus",
                columns: table => new
                {
                    MaMau = table.Column<int>(type: "int", nullable: false),
                    MaDanhMuc = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PhanLoaiMaus", x => new { x.MaMau, x.MaDanhMuc });
                    table.ForeignKey(
                        name: "FK_PhanLoaiMau_DanhMuc",
                        column: x => x.MaDanhMuc,
                        principalTable: "DanhMucMaus",
                        principalColumn: "MaDanhMuc",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PhanLoaiMau_MauCV",
                        column: x => x.MaMau,
                        principalTable: "MauCVs",
                        principalColumn: "MaMau",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CV_MaMau",
                table: "CV",
                column: "MaMau");

            migrationBuilder.CreateIndex(
                name: "IX_MauSacs_MaMau",
                table: "MauSacs",
                column: "MaMau");

            migrationBuilder.CreateIndex(
                name: "IX_PhanLoaiMaus_MaDanhMuc",
                table: "PhanLoaiMaus",
                column: "MaDanhMuc");

            migrationBuilder.AddForeignKey(
                name: "FK_Cv_MauCV",
                table: "CV",
                column: "MaMau",
                principalTable: "MauCVs",
                principalColumn: "MaMau",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Cv_MauCV",
                table: "CV");

            migrationBuilder.DropTable(
                name: "MauSacs");

            migrationBuilder.DropTable(
                name: "PhanLoaiMaus");

            migrationBuilder.DropTable(
                name: "DanhMucMaus");

            migrationBuilder.DropTable(
                name: "MauCVs");

            migrationBuilder.DropIndex(
                name: "IX_CV_MaMau",
                table: "CV");

            migrationBuilder.DropColumn(
                name: "MaHex",
                table: "CV");

            migrationBuilder.DropColumn(
                name: "MaMau",
                table: "CV");
        }
    }
}
