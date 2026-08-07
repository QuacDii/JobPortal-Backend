using System;
using System.Collections.Generic;

namespace TKVL.Models;

public partial class User
{
    public int MaUser { get; set; }

    public string Email { get; set; } = null!;

    public string? MatKhau { get; set; }

    public string HoTen { get; set; } = null!;

    public string? Avatar { get; set; }

    public string? GoogleId { get; set; }

    public string? FacebookId { get; set; }

    public byte VaiTro { get; set; }

    public decimal SoDuVi { get; set; }

    public bool TrangThai { get; set; }

    public DateTime NgayTao { get; set; }

    public int LuotXemCvConLai { get; set; } = 0;
    public DateTime? NgayHetHanGoi { get; set; }

    public virtual CongTy? CongTy { get; set; }

    public string? RefreshToken { get; set; }

    public string? ResetToken { get; set; }

    public DateTime? ResetTokenExpiry { get; set; }

    public DateTime? NgayHetHanRefreshToken { get; set; }

    public bool TrangThaiTimViec { get; set; } = false;

    public bool IsEmailVerified { get; set; } = false;

    public string? OtpCode { get; set; }

    public DateTime? OtpExpiry { get; set; }

    public virtual ICollection<Cv> Cvs { get; set; } = new List<Cv>();

    public virtual ICollection<GiaoDich> GiaoDiches { get; set; } = new List<GiaoDich>();

    public virtual ICollection<TinDaLuu> TinDaLuus { get; set; } = new List<TinDaLuu>();
    public virtual ICollection<UngVienDaLuu> UngVienDaLuus { get; set; } = new List<UngVienDaLuu>();
}
