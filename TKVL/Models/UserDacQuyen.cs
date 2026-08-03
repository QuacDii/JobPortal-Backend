using System;

namespace TKVL.Models;

public partial class UserDacQuyen
{
    public int MaUser { get; set; }
    public virtual User User { get; set; } = null!;

    public int MaDacQuyen { get; set; }
    public virtual DacQuyen DacQuyen { get; set; } = null!;

    public int? SoLuotConLai { get; set; } // Số lượt dùng còn lại (NULL nếu vô hạn)
    public DateTime NgayHetHan { get; set; } // Ngày hết hạn của đặc quyền
}