using System;
using System.Collections.Generic;

namespace TKVL.Models;

public partial class GiaoDich
{
    public int MaGd { get; set; }

    public int MaUser { get; set; }

    public int? MaGoi { get; set; }

    public byte LoaiGiaoDich { get; set; }

    public decimal SoTien { get; set; }

    public string PhuongThuc { get; set; } = null!;

    public string? MaGiaoDichDoiTac { get; set; }

    public DateTime NgayGd { get; set; }

    public bool TrangThai { get; set; }

    public virtual GoiDichVu? MaGoiNavigation { get; set; }

    public virtual User MaUserNavigation { get; set; } = null!;
}
