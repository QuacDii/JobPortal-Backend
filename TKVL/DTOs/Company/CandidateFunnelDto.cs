namespace TKVL.DTOs.Company;

public class CandidateFunnelDto
{
    public int MaDon { get; set; }
    public int MaUngVien { get; set; }
    public string HoTen { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? CvUrl { get; set; } // Link PDF của Cloudinary
    public string? ThuGioiThieu { get; set; }
    public DateTime NgayNop { get; set; }
    public byte TrangThai { get; set; } // 0: Mới, 1: Đã xem, 2: Hẹn PV, 3: Từ chối
    public string? GhiChu { get; set; }

    // 🌟 TỔNG QUAN AI
    public int? DiemMatchingTong { get; set; }
    public string? ProfileAiJson { get; set; }
    public bool IsPendingAi { get; set; }

    // 🌟 CÁC CHỈ SỐ AI MATCHING THÀNH PHẦN (MỚI BỔ SUNG)
    public int DiemKyNang { get; set; }
    public int DiemKinhNghiem { get; set; }
    public int DiemLinhVuc { get; set; }
    public int DiemCapBac { get; set; }
    public string? DiemManhTieuBieu { get; set; }
    public string? DiemConThieu { get; set; }
}