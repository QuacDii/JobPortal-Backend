using System;
using System.Collections.Generic;

namespace TKVL.Models;

public partial class KyNang
{
    public int MaKyNang { get; set; }

    public string TenKyNang { get; set; } = null!;

    public virtual ICollection<ChiTietViTri> MaViTris { get; set; } = new List<ChiTietViTri>();
}
