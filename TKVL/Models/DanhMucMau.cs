using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace TKVL.Models;

public partial class DanhMucMau
{
    [Key]
    public int MaDanhMuc { get; set; }

    [Required]
    [StringLength(100)]
    public string TenDanhMuc { get; set; } = null!;

    public virtual ICollection<PhanLoaiMau> PhanLoaiMaus { get; set; } = new List<PhanLoaiMau>();
}