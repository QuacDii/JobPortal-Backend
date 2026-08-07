using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TKVL.Migrations
{
    /// <inheritdoc />
    public partial class AddPackagePrivilegesSystem : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<byte>(
                name: "DoiTuongSuDung",
                table: "GoiDichVu",
                type: "tinyint",
                nullable: false,
                defaultValue: (byte)1);

            migrationBuilder.CreateTable(
                name: "DacQuyen",
                columns: table => new
                {
                    MaDacQuyen = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MaCode = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: false),
                    TenDacQuyen = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    DoiTuongSuDung = table.Column<byte>(type: "tinyint", nullable: false),
                    MoTa = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DacQuyen", x => x.MaDacQuyen);
                });

            migrationBuilder.CreateTable(
                name: "GoiDichVu_DacQuyen",
                columns: table => new
                {
                    MaGoi = table.Column<int>(type: "int", nullable: false),
                    MaDacQuyen = table.Column<int>(type: "int", nullable: false),
                    SoLuong = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GoiDichVu_DacQuyen", x => new { x.MaGoi, x.MaDacQuyen });
                    table.ForeignKey(
                        name: "FK_GoiDichVu_DacQuyen_DacQuyen_MaDacQuyen",
                        column: x => x.MaDacQuyen,
                        principalTable: "DacQuyen",
                        principalColumn: "MaDacQuyen",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_GoiDichVu_DacQuyen_GoiDichVu_MaGoi",
                        column: x => x.MaGoi,
                        principalTable: "GoiDichVu",
                        principalColumn: "maGoi",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "User_DacQuyen",
                columns: table => new
                {
                    MaUser = table.Column<int>(type: "int", nullable: false),
                    MaDacQuyen = table.Column<int>(type: "int", nullable: false),
                    SoLuotConLai = table.Column<int>(type: "int", nullable: true),
                    NgayHetHan = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_User_DacQuyen", x => new { x.MaUser, x.MaDacQuyen });
                    table.ForeignKey(
                        name: "FK_User_DacQuyen_DacQuyen_MaDacQuyen",
                        column: x => x.MaDacQuyen,
                        principalTable: "DacQuyen",
                        principalColumn: "MaDacQuyen",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_User_DacQuyen_User_MaUser",
                        column: x => x.MaUser,
                        principalTable: "User",
                        principalColumn: "maUser",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DacQuyen_MaCode",
                table: "DacQuyen",
                column: "MaCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GoiDichVu_DacQuyen_MaDacQuyen",
                table: "GoiDichVu_DacQuyen",
                column: "MaDacQuyen");

            migrationBuilder.CreateIndex(
                name: "IX_User_DacQuyen_MaDacQuyen",
                table: "User_DacQuyen",
                column: "MaDacQuyen");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "GoiDichVu_DacQuyen");

            migrationBuilder.DropTable(
                name: "User_DacQuyen");

            migrationBuilder.DropTable(
                name: "DacQuyen");

            migrationBuilder.DropColumn(
                name: "DoiTuongSuDung",
                table: "GoiDichVu");

            migrationBuilder.DropColumn(
                name: "TrangThai",
                table: "GoiDichVu");
        }
    }
}
