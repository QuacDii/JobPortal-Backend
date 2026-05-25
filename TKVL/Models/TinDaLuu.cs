using System;
using System.Collections.Generic;

namespace TKVL.Models;

public partial class TinDaLuu
{
    public int MaUser { get; set; }

    public int MaViTri { get; set; }

    public DateTime NgayLuu { get; set; }

    public virtual User MaUserNavigation { get; set; } = null!;

    public virtual ChiTietViTri MaViTriNavigation { get; set; } = null!;
}
