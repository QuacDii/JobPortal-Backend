using System;
using System.Collections.Generic;

namespace TKVL.Models;

public partial class Cv
{
    public int MaCv { get; set; }

    public int MaUser { get; set; }

    public string TieuDe { get; set; } = null!;

    public string? DuongDan { get; set; }

    public string? DuLieuCv { get; set; }

    public bool IsPublic { get; set; }

    public virtual ICollection<DonUngTuyen> DonUngTuyens { get; set; } = new List<DonUngTuyen>();

    public virtual User MaUserNavigation { get; set; } = null!;
}
