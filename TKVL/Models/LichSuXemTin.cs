using System;

namespace TKVL.Models;

public partial class LichSuXemTin
{
    public int MaLichSu { get; set; }
    public int MaTin { get; set; }
    public DateTime ThoiGianXem { get; set; } = DateTime.Now;

    public virtual TinTuyenDung MaTinNavigation { get; set; } = null!;
}