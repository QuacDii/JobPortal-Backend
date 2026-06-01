namespace TKVL.Models;

public partial class PhanLoaiMau
{
    public int MaMau { get; set; }
    public int MaDanhMuc { get; set; }

    public virtual MauCV MauCVNavigation { get; set; } = null!;
    public virtual DanhMucMau DanhMucMauNavigation { get; set; } = null!;
}