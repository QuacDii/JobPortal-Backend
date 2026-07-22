using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TKVL.Migrations
{
    /// <inheritdoc />
    public partial class OptimizeMauSacToMauCv : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DanhSachMau",
                table: "MauCVs",
                type: "nvarchar(255)",
                nullable: true);

            // 2. Chạy lệnh SQL để gom toàn bộ mã HEX từ bảng MauSacs đổ vào cột DanhSachMau (Ngăn cách bằng dấu phẩy)
            migrationBuilder.Sql(@"
                IF EXISTS (SELECT * FROM sys.tables WHERE name = 'MauSacs')
                BEGIN
                    EXEC('
                        UPDATE m
                        SET m.DanhSachMau = s.Colors
                        FROM MauCVs m
                        INNER JOIN (
                            SELECT MaMau, STRING_AGG(MaHex, '','') AS Colors
                            FROM MauSacs
                            GROUP BY MaMau
                        ) s ON m.MaMau = s.MaMau
                    ');
                END
            ");

            // 3. Xóa bảng MauSacs (SQL Server sẽ tự động drop Foreign Key FK_MauSac_MauCV liên quan)
            migrationBuilder.DropTable(
                name: "MauSacs");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MauSacs",
                columns: table => new
                {
                    MaMauSac = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MaMau = table.Column<int>(type: "int", nullable: false),
                    MaHex = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MauSacs", x => x.MaMauSac);
                    table.ForeignKey(
                        name: "FK_MauSac_MauCV",
                        column: x => x.MaMau,
                        principalTable: "MauCVs",
                        principalColumn: "MaMau",
                        onDelete: ReferentialAction.Cascade);
                });

            // Tách chuỗi từ cột DanhSachMau nạp ngược lại bảng MauSacs
            migrationBuilder.Sql(@"
                EXEC('
                    INSERT INTO MauSacs (MaMau, MaHex)
                    SELECT MaMau, value
                    FROM MauCVs
                    CROSS APPLY STRING_SPLIT(DanhSachMau, '','')
                    WHERE DanhSachMau IS NOT NULL
                ');
            ");

            // Xóa cột DanhSachMau khỏi bảng MauCVs
            migrationBuilder.DropColumn(
                name: "DanhSachMau",
                table: "MauCVs");
        }
    }
}
