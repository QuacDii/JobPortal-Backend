using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TKVL.Migrations
{
    /// <inheritdoc />
    public partial class AddMaNganhAndIsUploadedToCv : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            //migrationBuilder.DropTable(
            //    name: "MauSacs");

            

            migrationBuilder.AddColumn<bool>(
                name: "IsUploaded",
                table: "CV",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "MaNganh",
                table: "CV",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_CV_MaNganh",
                table: "CV",
                column: "MaNganh");

            migrationBuilder.AddForeignKey(
                name: "FK_CV_NganhNghe_MaNganh",
                table: "CV",
                column: "MaNganh",
                principalTable: "NganhNghe",
                principalColumn: "maNganh",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CV_NganhNghe_MaNganh",
                table: "CV");

            migrationBuilder.DropIndex(
                name: "IX_CV_MaNganh",
                table: "CV");


            migrationBuilder.DropColumn(
                name: "IsUploaded",
                table: "CV");

            migrationBuilder.DropColumn(
                name: "MaNganh",
                table: "CV");
        }
    }
}
