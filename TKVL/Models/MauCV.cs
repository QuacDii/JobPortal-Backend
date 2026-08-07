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

    public bool IsVip { get; set; }

    public bool IsATS { get; set; }

    public bool TrangThai { get; set; }

    public string? NgonNgu { get; set; } 

    public string? Tags { get; set; }

    public string DuLieuMau { get; set; }

    public string? LayoutJson { get; set; }

    public string? DanhSachMau { get; set; }

    public virtual ICollection<Cv> Cvs { get; set; } = new List<Cv>();
    public virtual ICollection<PhanLoaiMau> PhanLoaiMaus { get; set; } = new List<PhanLoaiMau>();
}