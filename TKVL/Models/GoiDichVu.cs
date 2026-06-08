using System;
using System.Collections.Generic;

namespace TKVL.Models;

public partial class GoiDichVu
{
    public int MaGoi { get; set; }

    public string TenGoi { get; set; } = null!;

    public byte LoaiGoi { get; set; }

    public decimal GiaTien { get; set; }

    public int? DonViThoiGian { get; set; }

    public int SoLuotXemCv { get; set; }
    public decimal? GiaKhuyenMai { get; set; }

    public virtual ICollection<GiaoDich> GiaoDiches { get; set; } = new List<GiaoDich>();
}
