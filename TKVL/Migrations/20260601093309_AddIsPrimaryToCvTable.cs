using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TKVL.Migrations
{
    /// <inheritdoc />
    public partial class AddIsPrimaryToCvTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsPrimary",
                table: "CV",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsPrimary",
                table: "CV");
        }
    }
}
