using System;
using System.Collections.Generic;

namespace TKVL.Models;

public partial class NganhNghe
{
    public int MaNganh { get; set; }

    public string TenNganh { get; set; } = null!;

    public bool TrangThai { get; set; }

    public virtual ICollection<ChiTietViTri> ChiTietViTris { get; set; } = new List<ChiTietViTri>();
}
