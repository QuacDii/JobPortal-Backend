namespace TKVL.DTOs.Company
{
    public class HuntCvDto
    {
        public int MaCv { get; set; }
        public string HoTen { get; set; }
        public string Email { get; set; } // Sẽ bị che mờ
        public string SoDienThoai { get; set; } // Sẽ bị che mờ
        public bool IsUnlocked { get; set; } // Flag để UI biết hiển thị nút Mở khóa hay thông tin thật
    }
}
