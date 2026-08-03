using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TKVL.Migrations
{
    /// <inheritdoc />
    public partial class AddUngVienDaLuuTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "UngVienDaLuu",
                columns: table => new
                {
                    maUser = table.Column<int>(type: "int", nullable: false),
                    maCV = table.Column<int>(type: "int", nullable: false),
                    ngayLuu = table.Column<DateTime>(type: "datetime", nullable: false, defaultValueSql: "(getdate())"),
                    ghiChuCaNhan = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UngVienDaLuu", x => new { x.maUser, x.maCV });
                    table.ForeignKey(
                        name: "FK_UngVienDaLuu_CV",
                        column: x => x.maCV,
                        principalTable: "CV",
                        principalColumn: "maCV",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UngVienDaLuu_User",
                        column: x => x.maUser,
                        principalTable: "User",
                        principalColumn: "maUser",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_UngVienDaLuu_maCV",
                table: "UngVienDaLuu",
                column: "maCV");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "UngVienDaLuu");
        }
    }
}
