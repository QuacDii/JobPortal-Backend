using System;
using System.Collections.Generic;

namespace TKVL.Models;

public partial class CongTy
{
    public int MaCongTy { get; set; }

    public int MaUser { get; set; }

    public string TenCongTy { get; set; } = null!;

    public string MaSoThue { get; set; } = null!;

    public string? QuyMo { get; set; }

    public string DiaChi { get; set; } = null!;

    public string? MoTa { get; set; }

    public string? Logo { get; set; }

    public bool TrangThai { get; set; }

    public virtual User MaUserNavigation { get; set; } = null!;

    public virtual ICollection<TinTuyenDung> TinTuyenDungs { get; set; } = new List<TinTuyenDung>();
}
