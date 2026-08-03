using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TKVL.Migrations
{
    /// <inheritdoc />
    public partial class RemoveSoLuotXemCVInGoiDichVu : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "soLuotXemCV",
                table: "GoiDichVu");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "soLuotXemCV",
                table: "GoiDichVu",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }
    }
}
