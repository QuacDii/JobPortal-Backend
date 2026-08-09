using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TKVL.Models;

public partial class Cv
{
    public int MaCv { get; set; }

    public int MaUser { get; set; }

    public int? MaMau { get; set; } 

    public string? MaHex { get; set; }

    public string TieuDe { get; set; } = null!;

    public string? DuongDan { get; set; }

    public DateTime? NgayCapNhat { get; set; }

    public string? DuLieuCv { get; set; }

    public bool IsPublic { get; set; }

    public bool IsPrimary { get; set; } = false;

    [MaxLength(10)]
    public string NgonNgu { get; set; }

    [MaxLength(50)]
    public string FontChu { get; set; }

    public bool IsUploaded { get; set; } = false;

    public int? MaNganhCon { get; set; }

    public string? CustomLayoutJson { get; set; }

    public virtual ICollection<CV_CauTrucMuc> CauTrucMucs { get; set; }

    [ForeignKey("MaNganhCon")]
    public virtual NganhNgheCon? NganhNgheCon { get; set; }

    public virtual ICollection<DonUngTuyen> DonUngTuyens { get; set; } = new List<DonUngTuyen>();

    public virtual User MaUserNavigation { get; set; } = null!;

    public virtual MauCV? MaMauNavigation { get; set; }
    public virtual ICollection<UngVienDaLuu> UngVienDaLuus { get; set; } = new List<UngVienDaLuu>();
}
