using System;
using System.Collections.Generic;

namespace TKVL.Models;

public partial class TinTuyenDung
{
    public int MaTin { get; set; }

    public int MaCongTy { get; set; }

    public string TieuDeChienDich { get; set; } = null!;

    public DateTime NgayHetHan { get; set; }

    public byte TrangThai { get; set; }

    public bool IsPromoted { get; set; }

    public virtual ICollection<ChiTietViTri> ChiTietViTris { get; set; } = new List<ChiTietViTri>();

    public virtual CongTy MaCongTyNavigation { get; set; } = null!;
}
