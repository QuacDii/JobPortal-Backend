using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace TKVL.Models;

public partial class MauCV
{
    [Key]
    public int MaMau { get; set; }

    [Required]
    [StringLength(100)]
    public string TenMau { get; set; } = null!;

    [StringLength(255)]
    public string? MoTa { get; set; }

    [StringLength(255)]
    public string? AnhThumbnail { get; set; }

    public bool IsATS { get; set; }

    public bool TrangThai { get; set; }

    // Liên kết 1-N (Một mẫu CV có nhiều CV con, nhiều bộ lọc, nhiều màu sắc)
    public virtual ICollection<Cv> Cvs { get; set; } = new List<Cv>();
    public virtual ICollection<PhanLoaiMau> PhanLoaiMaus { get; set; } = new List<PhanLoaiMau>();
    public virtual ICollection<MauSac_MauCV> MauSacs { get; set; } = new List<MauSac_MauCV>();
}