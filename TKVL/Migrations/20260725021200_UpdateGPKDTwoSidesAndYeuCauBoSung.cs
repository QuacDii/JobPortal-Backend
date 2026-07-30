using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TKVL.Migrations
{
    /// <inheritdoc />
    public partial class UpdateGPKDTwoSidesAndYeuCauBoSung : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "GiayPhepKinhDoanh",
                table: "CongTy",
                newName: "YeuCauBoSung");

            migrationBuilder.AddColumn<string>(
                name: "GiayPhepKinhDoanhMatSau",
                table: "CongTy",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GiayPhepKinhDoanhMatTruoc",
                table: "CongTy",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "GiayPhepKinhDoanhMatSau",
                table: "CongTy");

            migrationBuilder.DropColumn(
                name: "GiayPhepKinhDoanhMatTruoc",
                table: "CongTy");

            migrationBuilder.RenameColumn(
                name: "YeuCauBoSung",
                table: "CongTy",
                newName: "GiayPhepKinhDoanh");
        }
    }
}
