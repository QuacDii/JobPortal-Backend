using System;
using System.Collections.Generic;

namespace TKVL.Models;

public partial class ChiTietViTri
{
    public int MaViTri { get; set; }

    public int MaTin { get; set; }

    public int MaNganh { get; set; }

    public int MaPhuong { get; set; }

    public string TenViTri { get; set; } = null!;
    public string? CapBac { get; set; }

    public string Luong { get; set; } = null!;

    public string MoTaCongViec { get; set; } = null!;

    public string YeuCauUngVien { get; set; } = null!;

    public string QuyenLoi { get; set; } = null!;

    public int SoLuongTuyen { get; set; }
    public string? NganhNgheKhac { get; set; }

    public virtual ICollection<DonUngTuyen> DonUngTuyens { get; set; } = new List<DonUngTuyen>();

    public virtual NganhNghe MaNganhNavigation { get; set; } = null!;

    public virtual PhuongXa MaPhuongNavigation { get; set; } = null!;

    public virtual TinTuyenDung MaTinNavigation { get; set; } = null!;

    public virtual ICollection<TinDaLuu> TinDaLuus { get; set; } = new List<TinDaLuu>();

    public virtual ICollection<KyNang> MaKyNangs { get; set; } = new List<KyNang>();
}
