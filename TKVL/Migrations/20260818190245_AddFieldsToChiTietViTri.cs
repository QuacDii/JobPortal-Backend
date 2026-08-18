using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TKVL.Migrations
{
    /// <inheritdoc />
    public partial class AddFieldsToChiTietViTri : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "LyDoTuChoi",
                table: "ChiTietViTri",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "NgayHetHan",
                table: "ChiTietViTri",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<byte>(
                name: "TrangThai",
                table: "ChiTietViTri",
                type: "tinyint",
                nullable: false,
                defaultValue: (byte)0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LyDoTuChoi",
                table: "ChiTietViTri");

            migrationBuilder.DropColumn(
                name: "NgayHetHan",
                table: "ChiTietViTri");

            migrationBuilder.DropColumn(
                name: "TrangThai",
                table: "ChiTietViTri");
        }
    }
}
