using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TKVL.Migrations
{
    /// <inheritdoc />
    public partial class AddAdvancedFeaturesFixed : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "LuotXemCvConLai",
                table: "User",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "JobAlerts",
                columns: table => new
                {
                    MaAlert = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MaUser = table.Column<int>(type: "int", nullable: false),
                    MaNganh = table.Column<int>(type: "int", nullable: false),
                    TuKhoaKyNang = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TrangThai = table.Column<bool>(type: "bit", nullable: false),
                    MaNganhNavigationMaNganh = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_JobAlerts", x => x.MaAlert);
                    table.ForeignKey(
                        name: "FK_JobAlerts_NganhNghe_MaNganhNavigationMaNganh",
                        column: x => x.MaNganhNavigationMaNganh,
                        principalTable: "NganhNghe",
                        principalColumn: "maNganh",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_JobAlerts_User_MaUser",
                        column: x => x.MaUser,
                        principalTable: "User",
                        principalColumn: "maUser",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LichSuMoKhoaCvs",
                columns: table => new
                {
                    MaLichSu = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MaUser = table.Column<int>(type: "int", nullable: false),
                    MaCv = table.Column<int>(type: "int", nullable: false),
                    NgayMoKhoa = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LichSuMoKhoaCvs", x => x.MaLichSu);
                    table.ForeignKey(
                        name: "FK_LichSuMoKhoaCvs_CV_MaCv",
                        column: x => x.MaCv,
                        principalTable: "CV",
                        principalColumn: "maCV",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_LichSuMoKhoaCvs_User_MaUser",
                        column: x => x.MaUser,
                        principalTable: "User",
                        principalColumn: "maUser",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_JobAlerts_MaNganhNavigationMaNganh",
                table: "JobAlerts",
                column: "MaNganhNavigationMaNganh");

            migrationBuilder.CreateIndex(
                name: "IX_JobAlerts_MaUser",
                table: "JobAlerts",
                column: "MaUser");

            migrationBuilder.CreateIndex(
                name: "IX_LichSuMoKhoaCvs_MaCv",
                table: "LichSuMoKhoaCvs",
                column: "MaCv");

            migrationBuilder.CreateIndex(
                name: "IX_LichSuMoKhoaCvs_MaUser",
                table: "LichSuMoKhoaCvs",
                column: "MaUser");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "JobAlerts");

            migrationBuilder.DropTable(
                name: "LichSuMoKhoaCvs");

            migrationBuilder.DropColumn(
                name: "LuotXemCvConLai",
                table: "User");
        }
    }
}
