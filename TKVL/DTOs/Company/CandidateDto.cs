namespace TKVL.DTOs.Company
{
    public class CandidateDto
    {
        public int MaDon { get; set; }
        public string HoTen { get; set; }
        public string Email { get; set; }
        public string CvUrl { get; set; } // Link PDF của Cloudinary
        public string ThuGioiThieu { get; set; }
        public DateTime NgayNop { get; set; }
        public byte TrangThai { get; set; } // 0: Mới, 1: Đã xem, 2: Hẹn PV, 3: Từ chối
        public string GhiChu { get; set; }
        public int? DiemMatchingTong { get; set; }
        public string ProfileAiJson { get; set; }
    }
}
