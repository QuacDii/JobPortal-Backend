using System;
using System.ComponentModel.DataAnnotations;
namespace TKVL.Models;

public class JobAlert
{
    [Key]
    public int MaAlert { get; set; }
    public int MaUser { get; set; }
    public int MaNganh { get; set; }
    public string? TuKhoaKyNang { get; set; }
    public bool TrangThai { get; set; } = true;

    // Navigation properties
    public virtual User MaUserNavigation { get; set; } = null!;
    public virtual NganhNghe MaNganhNavigation { get; set; } = null!;
}