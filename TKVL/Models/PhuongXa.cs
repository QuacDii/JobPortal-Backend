using System;
using System.Collections.Generic;

namespace TKVL.Models;

public partial class PhuongXa
{
    public int MaPhuong { get; set; }

    public int MaTp { get; set; }

    public string TenPhuong { get; set; } = null!;

    public virtual ICollection<ChiTietViTri> ChiTietViTris { get; set; } = new List<ChiTietViTri>();

    public virtual ThanhPho MaTpNavigation { get; set; } = null!;
}
