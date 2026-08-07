using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TKVL.Migrations
{
    /// <inheritdoc />
    public partial class RemoveAiColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DanhGiaAi",
                table: "DonUngTuyen");

            migrationBuilder.DropColumn(
                name: "DiemPhuHop",
                table: "DonUngTuyen");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DanhGiaAi",
                table: "DonUngTuyen",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DiemPhuHop",
                table: "DonUngTuyen",
                type: "int",
                nullable: true);
        }
    }
}
