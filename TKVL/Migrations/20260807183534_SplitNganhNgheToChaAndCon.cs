using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TKVL.Migrations
{
    /// <inheritdoc />
    public partial class SplitNganhNgheToChaAndCon : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ChiTietViTri_NganhNghe",
                table: "ChiTietViTri");

            migrationBuilder.DropForeignKey(
                name: "FK_CV_NganhNghe_MaNganh",
                table: "CV");

            migrationBuilder.DropForeignKey(
                name: "FK_JobAlerts_NganhNghe_MaNganhNavigationMaNganh",
                table: "JobAlerts");

            migrationBuilder.DropTable(
                name: "NganhNghe");

            migrationBuilder.DropColumn(
                name: "MaNganh",
                table: "JobAlerts");

            migrationBuilder.RenameColumn(
                name: "MaNganhNavigationMaNganh",
                table: "JobAlerts",
                newName: "MaNganhCon");

            migrationBuilder.RenameIndex(
                name: "IX_JobAlerts_MaNganhNavigationMaNganh",
                table: "JobAlerts",
                newName: "IX_JobAlerts_MaNganhCon");

            migrationBuilder.RenameColumn(
                name: "MaNganh",
                table: "CV",
                newName: "maNganhCon");

            migrationBuilder.RenameIndex(
                name: "IX_CV_MaNganh",
                table: "CV",
                newName: "IX_CV_maNganhCon");

            migrationBuilder.RenameColumn(
                name: "KinhNghiem",
                table: "ChiTietViTri",
                newName: "kinhNghiem");

            migrationBuilder.RenameColumn(
                name: "CapBac",
                table: "ChiTietViTri",
                newName: "capBac");

            migrationBuilder.RenameColumn(
                name: "maNganh",
                table: "ChiTietViTri",
                newName: "maNganhCon");

            migrationBuilder.RenameIndex(
                name: "IX_ChiTietViTri_maNganh",
                table: "ChiTietViTri",
                newName: "IX_ChiTietViTri_maNganhCon");

            migrationBuilder.AlterColumn<string>(
                name: "capBac",
                table: "ChiTietViTri",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.CreateTable(
                name: "NganhNgheCha",
                columns: table => new
                {
                    maNganhCha = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    tenNganhCha = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NganhNgheCha", x => x.maNganhCha);
                });

            migrationBuilder.CreateTable(
                name: "NganhNgheCon",
                columns: table => new
                {
                    maNganhCon = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    tenNganhCon = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    maNganhCha = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NganhNgheCon", x => x.maNganhCon);
                    table.ForeignKey(
                        name: "FK_NganhNgheCon_NganhNgheCha",
                        column: x => x.maNganhCha,
                        principalTable: "NganhNgheCha",
                        principalColumn: "maNganhCha",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_NganhNgheCon_maNganhCha",
                table: "NganhNgheCon",
                column: "maNganhCha");

            migrationBuilder.AddForeignKey(
                name: "FK_ChiTietViTri_NganhNgheCon",
                table: "ChiTietViTri",
                column: "maNganhCon",
                principalTable: "NganhNgheCon",
                principalColumn: "maNganhCon");

            migrationBuilder.AddForeignKey(
                name: "FK_CV_NganhNgheCon_maNganhCon",
                table: "CV",
                column: "maNganhCon",
                principalTable: "NganhNgheCon",
                principalColumn: "maNganhCon",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_JobAlerts_NganhNgheCon_MaNganhCon",
                table: "JobAlerts",
                column: "MaNganhCon",
                principalTable: "NganhNgheCon",
                principalColumn: "maNganhCon",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ChiTietViTri_NganhNgheCon",
                table: "ChiTietViTri");

            migrationBuilder.DropForeignKey(
                name: "FK_CV_NganhNgheCon_maNganhCon",
                table: "CV");

            migrationBuilder.DropForeignKey(
                name: "FK_JobAlerts_NganhNgheCon_MaNganhCon",
                table: "JobAlerts");

            migrationBuilder.DropTable(
                name: "NganhNgheCon");

            migrationBuilder.DropTable(
                name: "NganhNgheCha");

            migrationBuilder.RenameColumn(
                name: "MaNganhCon",
                table: "JobAlerts",
                newName: "MaNganhNavigationMaNganh");

            migrationBuilder.RenameIndex(
                name: "IX_JobAlerts_MaNganhCon",
                table: "JobAlerts",
                newName: "IX_JobAlerts_MaNganhNavigationMaNganh");

            migrationBuilder.RenameColumn(
                name: "maNganhCon",
                table: "CV",
                newName: "MaNganh");

            migrationBuilder.RenameIndex(
                name: "IX_CV_maNganhCon",
                table: "CV",
                newName: "IX_CV_MaNganh");

            migrationBuilder.RenameColumn(
                name: "kinhNghiem",
                table: "ChiTietViTri",
                newName: "KinhNghiem");

            migrationBuilder.RenameColumn(
                name: "capBac",
                table: "ChiTietViTri",
                newName: "CapBac");

            migrationBuilder.RenameColumn(
                name: "maNganhCon",
                table: "ChiTietViTri",
                newName: "maNganh");

            migrationBuilder.RenameIndex(
                name: "IX_ChiTietViTri_maNganhCon",
                table: "ChiTietViTri",
                newName: "IX_ChiTietViTri_maNganh");

            migrationBuilder.AddColumn<int>(
                name: "MaNganh",
                table: "JobAlerts",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AlterColumn<string>(
                name: "CapBac",
                table: "ChiTietViTri",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100,
                oldNullable: true);

            migrationBuilder.CreateTable(
                name: "NganhNghe",
                columns: table => new
                {
                    maNganh = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MaNganhCha = table.Column<int>(type: "int", nullable: true),
                    tenNganh = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    trangThai = table.Column<bool>(type: "bit", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK__NganhNgh__4E0C021750B7BE5B", x => x.maNganh);
                    table.ForeignKey(
                        name: "FK_NganhNghe_NganhNghe_MaNganhCha",
                        column: x => x.MaNganhCha,
                        principalTable: "NganhNghe",
                        principalColumn: "maNganh",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_NganhNghe_MaNganhCha",
                table: "NganhNghe",
                column: "MaNganhCha");

            migrationBuilder.AddForeignKey(
                name: "FK_ChiTietViTri_NganhNghe",
                table: "ChiTietViTri",
                column: "maNganh",
                principalTable: "NganhNghe",
                principalColumn: "maNganh");

            migrationBuilder.AddForeignKey(
                name: "FK_CV_NganhNghe_MaNganh",
                table: "CV",
                column: "MaNganh",
                principalTable: "NganhNghe",
                principalColumn: "maNganh",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_JobAlerts_NganhNghe_MaNganhNavigationMaNganh",
                table: "JobAlerts",
                column: "MaNganhNavigationMaNganh",
                principalTable: "NganhNghe",
                principalColumn: "maNganh",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
