namespace TKVL.Dtos
{
    public class DangKyDto
    {
        public string Email { get; set; } = null!;
        public string MatKhau { get; set; } = null!;
        public string HoTen { get; set; } = null!;
        public byte VaiTro { get; set; } // 1: Nhà tuyển dụng, 2: Ứng viên
    }
}