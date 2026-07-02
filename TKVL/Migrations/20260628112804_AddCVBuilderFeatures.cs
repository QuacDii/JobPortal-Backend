using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TKVL.Migrations
{
    /// <inheritdoc />
    public partial class AddCVBuilderFeatures : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "FontChu",
                table: "CV",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "NgonNgu",
                table: "CV",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "CV_CauTrucMucs",
                columns: table => new
                {
                    MaCauTruc = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MaCV = table.Column<int>(type: "int", nullable: false),
                    LoaiMuc = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    TenMucHienThi = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ThuTu = table.Column<int>(type: "int", nullable: false),
                    IsVisible = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CV_CauTrucMucs", x => x.MaCauTruc);
                    table.ForeignKey(
                        name: "FK_CV_CauTrucMucs_CV_MaCV",
                        column: x => x.MaCV,
                        principalTable: "CV",
                        principalColumn: "maCV",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CV_CauTrucMucs_MaCV",
                table: "CV_CauTrucMucs",
                column: "MaCV");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CV_CauTrucMucs");

            migrationBuilder.DropColumn(
                name: "FontChu",
                table: "CV");

            migrationBuilder.DropColumn(
                name: "NgonNgu",
                table: "CV");
        }
    }
}
