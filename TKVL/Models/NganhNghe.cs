using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;

namespace TKVL.Models;

public partial class NganhNghe
{
    public int MaNganh { get; set; }

    public string TenNganh { get; set; } = null!;

    public bool TrangThai { get; set; }

    public int? MaNganhCha { get; set; }

    [ForeignKey("MaNganhCha")]
    public virtual NganhNghe? NganhCha { get; set; }

    public virtual ICollection<NganhNghe> NganhCon { get; set; } = new List<NganhNghe>();

    public virtual ICollection<ChiTietViTri> ChiTietViTris { get; set; } = new List<ChiTietViTri>();
}
