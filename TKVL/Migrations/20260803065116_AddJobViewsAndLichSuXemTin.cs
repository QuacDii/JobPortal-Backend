using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TKVL.Migrations
{
    /// <inheritdoc />
    public partial class AddJobViewsAndLichSuXemTin : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "luotXem",
                table: "TinTuyenDung",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "LichSuXemTin",
                columns: table => new
                {
                    maLichSu = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    maTin = table.Column<int>(type: "int", nullable: false),
                    thoiGianXem = table.Column<DateTime>(type: "datetime", nullable: false, defaultValueSql: "GETDATE()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LichSuXemTin", x => x.maLichSu);
                    table.ForeignKey(
                        name: "FK_LichSuXemTin_TinTuyenDung",
                        column: x => x.maTin,
                        principalTable: "TinTuyenDung",
                        principalColumn: "maTin",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LichSuXemTin_maTin",
                table: "LichSuXemTin",
                column: "maTin");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LichSuXemTin");

            migrationBuilder.DropColumn(
                name: "luotXem",
                table: "TinTuyenDung");
        }
    }
}
