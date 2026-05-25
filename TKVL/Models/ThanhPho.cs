using System;
using System.Collections.Generic;

namespace TKVL.Models;

public partial class ThanhPho
{
    public int MaTp { get; set; }

    public string TenTp { get; set; } = null!;

    public virtual ICollection<PhuongXa> PhuongXas { get; set; } = new List<PhuongXa>();
}
