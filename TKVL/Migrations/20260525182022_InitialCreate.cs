using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TKVL.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "GoiDichVu",
                columns: table => new
                {
                    maGoi = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    tenGoi = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    loaiGoi = table.Column<byte>(type: "tinyint", nullable: false),
                    giaTien = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    soNgayHieuLuc = table.Column<int>(type: "int", nullable: false),
                    soLuotXemCV = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__GoiDichV__2D87A9A0DF458666", x => x.maGoi);
                });

            migrationBuilder.CreateTable(
                name: "KyNang",
                columns: table => new
                {
                    maKyNang = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    tenKyNang = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__KyNang__A5BBD21FE9BCB984", x => x.maKyNang);
                });

            migrationBuilder.CreateTable(
                name: "NganhNghe",
                columns: table => new
                {
                    maNganh = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    tenNganh = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    trangThai = table.Column<bool>(type: "bit", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__NganhNgh__4E0C021750B7BE5B", x => x.maNganh);
                });

            migrationBuilder.CreateTable(
                name: "ThanhPho",
                columns: table => new
                {
                    maTP = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    tenTP = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__ThanhPho__7A22625BAD627896", x => x.maTP);
                });

            migrationBuilder.CreateTable(
                name: "User",
                columns: table => new
                {
                    maUser = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    email = table.Column<string>(type: "varchar(100)", unicode: false, maxLength: 100, nullable: false),
                    matKhau = table.Column<string>(type: "varchar(255)", unicode: false, maxLength: 255, nullable: true),
                    hoTen = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    avatar = table.Column<string>(type: "varchar(255)", unicode: false, maxLength: 255, nullable: true),
                    googleId = table.Column<string>(type: "varchar(100)", unicode: false, maxLength: 100, nullable: true),
                    facebookId = table.Column<string>(type: "varchar(100)", unicode: false, maxLength: 100, nullable: true),
                    vaiTro = table.Column<byte>(type: "tinyint", nullable: false, defaultValue: (byte)2),
                    soDuVi = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    trangThai = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    ngayTao = table.Column<DateTime>(type: "datetime", nullable: false, defaultValueSql: "(getdate())")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__User__18B21FF1C0EDDC7B", x => x.maUser);
                });

            migrationBuilder.CreateTable(
                name: "PhuongXa",
                columns: table => new
                {
                    maPhuong = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    maTP = table.Column<int>(type: "int", nullable: false),
                    tenPhuong = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__PhuongXa__DF98DF6714DE7A6F", x => x.maPhuong);
                    table.ForeignKey(
                        name: "FK_PhuongXa_ThanhPho",
                        column: x => x.maTP,
                        principalTable: "ThanhPho",
                        principalColumn: "maTP",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CongTy",
                columns: table => new
                {
                    maCongTy = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    maUser = table.Column<int>(type: "int", nullable: false),
                    tenCongTy = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    maSoThue = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: false),
                    quyMo = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    diaChi = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    moTa = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    logo = table.Column<string>(type: "varchar(255)", unicode: false, maxLength: 255, nullable: true),
                    trangThai = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__CongTy__EAECFE7B76A7CB70", x => x.maCongTy);
                    table.ForeignKey(
                        name: "FK_CongTy_User",
                        column: x => x.maUser,
                        principalTable: "User",
                        principalColumn: "maUser",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CV",
                columns: table => new
                {
                    maCV = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    maUser = table.Column<int>(type: "int", nullable: false),
                    tieuDe = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    duongDan = table.Column<string>(type: "varchar(255)", unicode: false, maxLength: 255, nullable: true),
                    duLieuCV = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    isPublic = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__CV__7A3E0CF073B2E13F", x => x.maCV);
                    table.ForeignKey(
                        name: "FK_CV_User",
                        column: x => x.maUser,
                        principalTable: "User",
                        principalColumn: "maUser",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "GiaoDich",
                columns: table => new
                {
                    maGD = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    maUser = table.Column<int>(type: "int", nullable: false),
                    maGoi = table.Column<int>(type: "int", nullable: true),
                    loaiGiaoDich = table.Column<byte>(type: "tinyint", nullable: false),
                    soTien = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    phuongThuc = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: false),
                    maGiaoDichDoiTac = table.Column<string>(type: "varchar(100)", unicode: false, maxLength: 100, nullable: true),
                    ngayGD = table.Column<DateTime>(type: "datetime", nullable: false, defaultValueSql: "(getdate())"),
                    trangThai = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__GiaoDich__7A3E2D67EF992E32", x => x.maGD);
                    table.ForeignKey(
                        name: "FK_GiaoDich_GoiDichVu",
                        column: x => x.maGoi,
                        principalTable: "GoiDichVu",
                        principalColumn: "maGoi");
                    table.ForeignKey(
                        name: "FK_GiaoDich_User",
                        column: x => x.maUser,
                        principalTable: "User",
                        principalColumn: "maUser");
                });

            migrationBuilder.CreateTable(
                name: "TinTuyenDung",
                columns: table => new
                {
                    maTin = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    maCongTy = table.Column<int>(type: "int", nullable: false),
                    tieuDeChienDich = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ngayHetHan = table.Column<DateTime>(type: "datetime", nullable: false),
                    trangThai = table.Column<byte>(type: "tinyint", nullable: false),
                    isPromoted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__TinTuyen__0FC4052A75963FBB", x => x.maTin);
                    table.ForeignKey(
                        name: "FK_TinTuyenDung_CongTy",
                        column: x => x.maCongTy,
                        principalTable: "CongTy",
                        principalColumn: "maCongTy",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ChiTietViTri",
                columns: table => new
                {
                    maViTri = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    maTin = table.Column<int>(type: "int", nullable: false),
                    maNganh = table.Column<int>(type: "int", nullable: false),
                    maPhuong = table.Column<int>(type: "int", nullable: false),
                    tenViTri = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    luong = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    moTaCongViec = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    yeuCauUngVien = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    quyenLoi = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    soLuongTuyen = table.Column<int>(type: "int", nullable: false, defaultValue: 1)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__ChiTietV__1D9EB9AFB5B463F0", x => x.maViTri);
                    table.ForeignKey(
                        name: "FK_ChiTietViTri_NganhNghe",
                        column: x => x.maNganh,
                        principalTable: "NganhNghe",
                        principalColumn: "maNganh");
                    table.ForeignKey(
                        name: "FK_ChiTietViTri_PhuongXa",
                        column: x => x.maPhuong,
                        principalTable: "PhuongXa",
                        principalColumn: "maPhuong");
                    table.ForeignKey(
                        name: "FK_ChiTietViTri_TinTuyenDung",
                        column: x => x.maTin,
                        principalTable: "TinTuyenDung",
                        principalColumn: "maTin",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DonUngTuyen",
                columns: table => new
                {
                    maDon = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    maCV = table.Column<int>(type: "int", nullable: false),
                    maViTri = table.Column<int>(type: "int", nullable: false),
                    thuGioiThieu = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ghiChu = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    ngayNop = table.Column<DateTime>(type: "datetime", nullable: false, defaultValueSql: "(getdate())"),
                    trangThai = table.Column<byte>(type: "tinyint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__DonUngTu__2431086DFCC52540", x => x.maDon);
                    table.ForeignKey(
                        name: "FK_DonUngTuyen_CV",
                        column: x => x.maCV,
                        principalTable: "CV",
                        principalColumn: "maCV");
                    table.ForeignKey(
                        name: "FK_DonUngTuyen_ChiTietViTri",
                        column: x => x.maViTri,
                        principalTable: "ChiTietViTri",
                        principalColumn: "maViTri",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TinDaLuu",
                columns: table => new
                {
                    maUser = table.Column<int>(type: "int", nullable: false),
                    maViTri = table.Column<int>(type: "int", nullable: false),
                    ngayLuu = table.Column<DateTime>(type: "datetime", nullable: false, defaultValueSql: "(getdate())")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__TinDaLuu__F96BF46BF9E6D8A3", x => new { x.maUser, x.maViTri });
                    table.ForeignKey(
                        name: "FK_TinDaLuu_ChiTietViTri",
                        column: x => x.maViTri,
                        principalTable: "ChiTietViTri",
                        principalColumn: "maViTri",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TinDaLuu_User",
                        column: x => x.maUser,
                        principalTable: "User",
                        principalColumn: "maUser");
                });

            migrationBuilder.CreateTable(
                name: "ViTri_KyNang",
                columns: table => new
                {
                    maViTri = table.Column<int>(type: "int", nullable: false),
                    maKyNang = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__ViTri_Ky__F7C5048EA33CCCA9", x => new { x.maViTri, x.maKyNang });
                    table.ForeignKey(
                        name: "FK_ViTriKyNang_ChiTietViTri",
                        column: x => x.maViTri,
                        principalTable: "ChiTietViTri",
                        principalColumn: "maViTri",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ViTriKyNang_KyNang",
                        column: x => x.maKyNang,
                        principalTable: "KyNang",
                        principalColumn: "maKyNang",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ChiTietViTri_maNganh",
                table: "ChiTietViTri",
                column: "maNganh");

            migrationBuilder.CreateIndex(
                name: "IX_ChiTietViTri_maPhuong",
                table: "ChiTietViTri",
                column: "maPhuong");

            migrationBuilder.CreateIndex(
                name: "IX_ChiTietViTri_maTin",
                table: "ChiTietViTri",
                column: "maTin");

            migrationBuilder.CreateIndex(
                name: "UQ__CongTy__18B21FF0E38144C8",
                table: "CongTy",
                column: "maUser",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UQ__CongTy__6A0F0170C6948BB6",
                table: "CongTy",
                column: "maSoThue",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CV_maUser",
                table: "CV",
                column: "maUser");

            migrationBuilder.CreateIndex(
                name: "IX_DonUngTuyen_maCV",
                table: "DonUngTuyen",
                column: "maCV");

            migrationBuilder.CreateIndex(
                name: "IX_DonUngTuyen_maViTri",
                table: "DonUngTuyen",
                column: "maViTri");

            migrationBuilder.CreateIndex(
                name: "IX_GiaoDich_maGoi",
                table: "GiaoDich",
                column: "maGoi");

            migrationBuilder.CreateIndex(
                name: "IX_GiaoDich_maUser",
                table: "GiaoDich",
                column: "maUser");

            migrationBuilder.CreateIndex(
                name: "IX_PhuongXa_maTP",
                table: "PhuongXa",
                column: "maTP");

            migrationBuilder.CreateIndex(
                name: "IX_TinDaLuu_maViTri",
                table: "TinDaLuu",
                column: "maViTri");

            migrationBuilder.CreateIndex(
                name: "IX_TinTuyenDung_maCongTy",
                table: "TinTuyenDung",
                column: "maCongTy");

            migrationBuilder.CreateIndex(
                name: "UQ__User__0DA2E4829AC45A41",
                table: "User",
                column: "googleId",
                unique: true,
                filter: "[googleId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "UQ__User__1DCE2AA87028DFD3",
                table: "User",
                column: "facebookId",
                unique: true,
                filter: "[facebookId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "UQ__User__AB6E61649BE61696",
                table: "User",
                column: "email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ViTri_KyNang_maKyNang",
                table: "ViTri_KyNang",
                column: "maKyNang");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DonUngTuyen");

            migrationBuilder.DropTable(
                name: "GiaoDich");

            migrationBuilder.DropTable(
                name: "TinDaLuu");

            migrationBuilder.DropTable(
                name: "ViTri_KyNang");

            migrationBuilder.DropTable(
                name: "CV");

            migrationBuilder.DropTable(
                name: "GoiDichVu");

            migrationBuilder.DropTable(
                name: "ChiTietViTri");

            migrationBuilder.DropTable(
                name: "KyNang");

            migrationBuilder.DropTable(
                name: "NganhNghe");

            migrationBuilder.DropTable(
                name: "PhuongXa");

            migrationBuilder.DropTable(
                name: "TinTuyenDung");

            migrationBuilder.DropTable(
                name: "ThanhPho");

            migrationBuilder.DropTable(
                name: "CongTy");

            migrationBuilder.DropTable(
                name: "User");
        }
    }
}
