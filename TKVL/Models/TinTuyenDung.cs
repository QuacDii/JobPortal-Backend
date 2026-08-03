using System;
using System.Collections.Generic;

namespace TKVL.Models;

public partial class TinTuyenDung
{
    public int MaTin { get; set; }

    public int MaCongTy { get; set; }

    public string TieuDeChienDich { get; set; } = null!;

    public DateTime NgayHetHan { get; set; }

    public byte TrangThai { get; set; } // 0: Nháp/Chờ duyệt | 1: Đang đăng | 2: Tạm dừng/Ẩn | 3: Hết hạn

    public bool IsPromoted { get; set; }

    public DateTime NgayDang { get; set; } = DateTime.Now;

    public int LuotXem { get; set; } = 0;

    public virtual ICollection<ChiTietViTri> ChiTietViTris { get; set; } = new List<ChiTietViTri>();

    public virtual CongTy MaCongTyNavigation { get; set; } = null!;

    public virtual ICollection<LichSuXemTin> LichSuXemTins { get; set; } = new List<LichSuXemTin>();
}