namespace TKVL.Models;

public partial class GoiDichVu_DacQuyen
{
    public int MaGoi { get; set; }
    public virtual GoiDichVu GoiDichVu { get; set; } = null!;

    public int MaDacQuyen { get; set; }
    public virtual DacQuyen DacQuyen { get; set; } = null!;

    public int? SoLuong { get; set; } // Nullable: Nếu NULL là quyền vô hạn/được mở khóa feature
}