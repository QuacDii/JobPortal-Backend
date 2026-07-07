using System;
using System.Collections.Generic;

namespace TKVL.Models;

public partial class DonUngTuyen
{
    public int MaDon { get; set; }

    public int MaCv { get; set; }

    public int MaViTri { get; set; }

    public string? ThuGioiThieu { get; set; }

    public string? GhiChu { get; set; }

    public DateTime NgayNop { get; set; }

    public byte TrangThai { get; set; }

    public virtual Cv MaCvNavigation { get; set; } = null!;

    public virtual ChiTietViTri MaViTriNavigation { get; set; } = null!;
    // Thêm dòng này vào class DonUngTuyen
    public virtual ChiTietPhanTichAi? ChiTietPhanTichAi { get; set; }
}
