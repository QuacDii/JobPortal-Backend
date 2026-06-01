using System;
using System.ComponentModel.DataAnnotations;
namespace TKVL.Models;

public class LichSuMoKhoaCV
{
    [Key]
    public int MaLichSu { get; set; }
    public int MaUser { get; set; }
    public int MaCv { get; set; }
    public DateTime NgayMoKhoa { get; set; } = DateTime.Now;

    // Navigation properties
    public virtual User MaUserNavigation { get; set; } = null!;
    public virtual Cv MaCvNavigation { get; set; } = null!;
}