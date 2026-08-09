using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TKVL.Models;

public class JobAlert
{
    [Key]
    public int MaAlert { get; set; }
    public int MaUser { get; set; }

    public int MaNganhCon { get; set; }

    public string? TuKhoaKyNang { get; set; }
    public bool TrangThai { get; set; } = true;

    // Navigation properties
    [ForeignKey("MaUser")]
    public virtual User MaUserNavigation { get; set; } = null!;

    [ForeignKey("MaNganhCon")]
    public virtual NganhNgheCon? MaNganhConNavigation { get; set; }
}