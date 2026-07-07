using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TKVL.Migrations
{
    /// <inheritdoc />
    public partial class AddDynamicLayoutJsonFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "LayoutJson",
                table: "MauCVs",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CustomLayoutJson",
                table: "CV",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LayoutJson",
                table: "MauCVs");

            migrationBuilder.DropColumn(
                name: "CustomLayoutJson",
                table: "CV");
        }
    }
}
